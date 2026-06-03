# W_API — API d'ingestion de documents Pour le deposer dans la bd de AQManager

API ASP.NET Core (.NET 10) pour lire des PDF, identifier leur numéro de référence et les rattacher automatiquement aux enregistrements de la GMAO AQManager (LRA / Afridigital).

---

## Prérequis

### 1. .NET SDK
- .NET 10 SDK ou supérieur : https://dotnet.microsoft.com/download

### 2. SQL Server
- Instance SQL Server accessible avec la base **NAME BD**
- Un utilisateur technique dans AQManager (noter son ID → `User:TechnicalUserId`)

### 3. Tesseract OCR
Télécharger les binaires et les données de langue :

**Windows :**
```
winget install UB-Mannheim.TesseractOCR
```
ou depuis https://github.com/UB-Mannheim/tesseract/wiki

**Fichiers de langue requis** (`fra.traineddata` + `eng.traineddata`) :
```
https://github.com/tesseract-ocr/tessdata/raw/main/fra.traineddata
https://github.com/tesseract-ocr/tessdata/raw/main/eng.traineddata
```
Copier ces fichiers dans le dossier configuré dans `Ocr:TessDataPath` (ex. `C:\tessdata\`).

---

## Configuration

Éditer **`W_API.Api/appsettings.json`** :

| Clé | Description |
|---|---|
| `ConnectionStrings:AqManager` | Chaîne de connexion SQL Server vers AQManagerData |
| `Paths:InputFolder` | Dossier de dépôt des PDF à traiter |
| `Paths:DocsPhysicalFolder` | Chemin disque du dossier `/Docs/` du serveur AQManager |
| `Paths:ToVerifyFolder` | Dossier de quarantaine (fichiers non rattachables) |
| `Ingestion:MaxFilesPerRun` | Max fichiers par appel `/run` (commencer à 5) |
| `Ocr:TessDataPath` | Chemin vers le dossier contenant `*.traineddata` |
| `Ocr:Languages` | Langues Tesseract (ex. `fra+eng`) |
| `Ocr:MinConfidence` | Seuil de confiance OCR 0–100 (défaut : 60) |
| `User:TechnicalUserId` | ID utilisateur AQManager pour `CreatedBy`/`ModifiedBy` |
| `Ingestion:EnableBackgroundWorker` | `true` pour activer la scrutation automatique |

### Section DocumentTypes
Chaque entrée définit un type de document :
```json
{
  "Name": "Bon de commande",
  "Regex": "(?<site>TO|AB|SO|TA)?/?BC[-]?(?<yy>\\d{2})[-]?(?<seq>\\d{4,6})",
  "TableName": "AQManagerData.PurchaseOrders,AQManagerData",
  "DocumentCategoryId": 11,
  "NumberColumns": ["PONumber"]
}
```
- L'ordre dans la liste détermine la priorité de détection
- `NumberColumns` : colonnes testées dans l'ordre (ex. factures : `SISupplierNumber` d'abord)

---

## Lancement

```bash
cd W_API.Api
dotnet run
```

L'API démarre sur `https://localhost:5001` (ou port affiché dans la console).

Au démarrage, l'API vérifie automatiquement :
- La connexion SQL Server
- L'accès en écriture au dossier `/Docs/`
- La disponibilité de Tesseract

---

## Endpoints

### `GET /api/health`
Vérifie l'état de tous les composants.

```bash
curl https://localhost:5001/api/health
```

### `POST /api/ingestion/run`
Lance le traitement par lot (lit `MaxFilesPerRun` PDF depuis `InputFolder`).

```bash
curl -X POST https://localhost:5001/api/ingestion/run
```

Réponse :
```json
{
  "batchId": "A3F2B1C0",
  "total": 5,
  "rattaches": 3,
  "doublons": 1,
  "aVerifier": 1,
  "erreurs": []
}
```

### `POST /api/ingestion/file`
Traite un seul fichier PDF (upload multipart).

```bash
curl -X POST https://localhost:5001/api/ingestion/file \
     -F "file=@/chemin/vers/BC-26-00001.pdf"
```

### `GET /api/ingestion/status/{batchId}`
Consulte l'état d'un lot précédent.

```bash
curl https://localhost:5001/api/ingestion/status/A3F2B1C0
```

---

## Workflow recommandé au démarrage

1. **Configurer** `appsettings.json` (connexion SQL, chemins, tessdata)
2. **Lancer** `GET /api/health` → tous les checks doivent être `ok: true`
3. **Déposer 5 PDF** dans `InputFolder`
4. **Appeler** `POST /api/ingestion/run`
5. **Vérifier** le récapitulatif dans la console et les logs (`logs/ingestion-*.log`)
6. **Contrôler** dans AQManager que les documents apparaissent bien rattachés
7. **Augmenter** `MaxFilesPerRun` progressivement une fois le matching validé

---

## Structure du projet

```
W_API/
├── W_API.Api/                    ← ASP.NET Core WebAPI (Program.cs, Controllers)
├── W_API.Core/                   ← Modèles, interfaces, services métier
│   ├── Configuration/            ← AppSettings (IOptions)
│   ├── Interfaces/               ← Contrats de service
│   ├── Models/                   ← DTO métier
│   └── Services/                 ← IngestionService, DocumentTypeDetector, SiteResolver
├── W_API.Infrastructure/         ← Implémentations techniques
│   ├── Data/                     ← AqManagerRepository (Dapper + SqlClient)
│   ├── IO/                       ← FileManager
│   ├── Ocr/                      ← OcrEngine (Tesseract)
│   ├── Pdf/                      ← PdfTextExtractor (PdfPig)
│   └── BackgroundWorker/         ← IngestionBackgroundService
├── W_API.Tests/                  ← Tests xUnit (Moq + FluentAssertions)
└── sql/
    └── create_ingestion_log.sql  ← Script base AQManager_Logs
```

---

## Logs

Les logs sont écrits dans `logs/ingestion-YYYYMMDD.log` et dans la console.

Format terminal :
```
[14:32:01 INF] --- Traitement : TO_BC-26-00001.pdf
[14:32:01 INF]   Numéro : TO/BC-26-00001 | Type : Bon de commande | Table : AQManagerData.PurchaseOrders,AQManagerData
[14:32:01 INF]   RecordID=42 (colonne 'PONumber', site=2)
[14:32:01 INF]   ✓ Rattaché : TO_BC-26-00001.pdf → Documents.ID liée à AQManagerData.PurchaseOrders,AQManagerData #42
```

---

## Points à vérifier dans AQManagerData

Avant le premier run :
1. Confirmer le nom réel de la colonne de numéro dans `ReceivingSlips` (probablement `RSNumber`)
2. Vérifier que l'utilisateur technique (`TechnicalUserId`) existe dans la table `Users`
3. Confirmer que les catégories (IDs 9, 10, 11, 13) existent dans `DocumentsCategories`
4. Vérifier les droits de l'utilisateur SQL sur la table `Documents` (INSERT) et les tables métier (SELECT)
