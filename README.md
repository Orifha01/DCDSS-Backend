# Digital Clinical Decision Support System (DCDSS)

## 1. Title & Overview

**Digital Clinical Decision Support System (DCDSS) for Rural South African Hospitals**

A .NET 8 Web API backend designed to improve clinical outcomes in under-resourced rural South African hospital settings. DCDSS provides:

- **Clinical Decision Support (CDS)** — rule-driven diagnostic recommendations, drug-drug interaction warnings, allergy conflict detection, and critical-lab alerts evaluated against a patient's stored records and/or explicit inputs.
- **Lab Management** — ordering tests, uploading results, and automatic classification (Normal / High / Low / Indeterminate).
- **Medication Safety** — prescription gateway that blocks drugs with Critical conflicts (allergy, drug-drug interaction, drug-lab) and surfaces non-critical warnings for clinician review.
- **POPIA-Compliant Audit Logging** — automatic audit trail for every sensitive action (mutations and PHI reads) via an action filter. Audit `Details` stores only identifiers and a success flag — never clinical text, passwords, or PII.

Supported roles: `Doctor`, `Nurse`, `LaboratoryStaff`, `Administrator`.

---

## 2. Prerequisites

| Dependency | Version | Notes |
|---|---|---|
| .NET 8 SDK | 8.0.427 or later | https://dotnet.microsoft.com/download |
| MySQL | 8.0.x | Local or remote instance |
| dotnet-ef (EF Core CLI) | 8.0.33 | See install command below |

Install the EF Core CLI tool:

```powershell
dotnet tool install --global dotnet-ef --version 8.0.33
```

---

## 3. Restore & Build

Run the following commands from the repository root:

```powershell
cd 'c:\Users\nm\IdeaProjects\Digital Clinical Decision Support System (DCDSS)'
dotnet restore
dotnet build
```

The build must complete with 0 errors. No database connection is required to build.

---

## 4. Configure MySQL

Edit `DCDSS/appsettings.json` and update the `DefaultConnection` string:

```json
"ConnectionStrings": {
  "DefaultConnection": "Server=localhost;Port=3306;Database=DCDSS;User=root;Password=yourpassword;AllowPublicKeyRetrieval=True;"
}
```

**`AllowPublicKeyRetrieval=True`** is required for MySQL 8's `caching_sha2_password` authentication plugin.

Create the database before running migrations:

```sql
CREATE DATABASE DCDSS;
```

---

## 5. EF Core Migrations

Ensure `dotnet-ef` is installed (see Prerequisites), then run:

```powershell
cd 'c:\Users\nm\IdeaProjects\Digital Clinical Decision Support System (DCDSS)\DCDSS'
$env:ASPNETCORE_ENVIRONMENT = 'Development'
dotnet ef migrations add InitialCreate
dotnet ef database update
```

> **Note:** The project uses `new MySqlServerVersion(new Version(8, 0, 34))` instead of `ServerVersion.AutoDetect(...)`, so migrations scaffold without a live database connection.

---

## 6. Initial Administrator

A default administrator account is seeded automatically on first startup when no `Administrator` user exists in the database.

**Development** (`appsettings.Development.json` override):
- Username: `admin`
- Password: `Admin@12345`

**Production** (`AdminSeed:Password` is empty in `appsettings.json`):
- The seeder generates a cryptographically random password, logs it **once** at `Warning` level, and writes it to `.admin-seed-password.txt` in the working directory.
- Capture the password from the console log or that file immediately and rotate it — the file should be deleted and kept out of source control.

**Bootstrap flow:**
1. The seeded admin account is created on startup if no admin exists (idempotent — runs on every startup, no-op when an admin already exists).
2. Log in with the seeded credentials via `POST /api/auth/login`.
3. Use the returned JWT to call `POST /api/auth/register` (Administrator role required) to create staff accounts.

> **Note:** There is no forced-password-change flow in this version. Password rotation after first login is the operator's responsibility.

---

## 7. Run the API

```powershell
cd 'c:\Users\nm\IdeaProjects\Digital Clinical Decision Support System (DCDSS)'
dotnet run --project DCDSS
```

| URL | Description |
|---|---|
| `https://localhost:{port}/swagger` | Swagger UI (Development environment only) |
| `/health` | Health check endpoint |

---

## 8. Authentication

All endpoints except `POST /api/auth/login` require a JWT Bearer token.

**Step 1 — Get a token:**

```bash
curl -X POST https://localhost:{port}/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"admin","password":"Admin@12345"}'
```

The response includes a `token` field.

**Step 2 — Authorize in Swagger:**
Click the **Authorize** button in Swagger UI and enter:

```
Bearer <token>
```

**Step 3 — Use the token in requests:**

```
Authorization: Bearer <token>
```

**Production security note:** The default `JwtSettings:SigningKey` in `appsettings.json` is the placeholder `REPLACE_ME`. The application **will not start** if this placeholder is present — the startup validator rejects it. Override the key via an environment variable or secrets manager before deploying:

```
JwtSettings__SigningKey=<your-random-32+-byte-key>
```

The signing key must be at least 32 UTF-8 bytes and must not equal `REPLACE_ME`.

---

## 9. Endpoint Reference

Create a `MedicalRecord` (via `POST /api/medical-records`) before ordering a lab test or prescribing a medication — both `LabResult.RecordId` and `Medication.RecordId` are required foreign keys to `MedicalRecords`.

| Method | Path | Roles | Request Body | Notes |
|---|---|---|---|---|
| `POST` | `/api/auth/register` | Administrator | `RegisterDto` | Returns `UserDto` |
| `POST` | `/api/auth/login` | Anonymous | `LoginDto` | Returns JWT and role |
| `POST` | `/api/medical-records` | Doctor, Nurse | `CreateMedicalRecordDto` | Required before lab order/medication; returns `MedicalRecordDto` |
| `GET` | `/api/patients` | Doctor, Nurse, LaboratoryStaff | — | Paginated; `?page&pageSize&search` |
| `POST` | `/api/patients` | Doctor, Nurse | `CreatePatientDto` | Returns `PatientDetailDto` |
| `GET` | `/api/patients/{id}` | Doctor, Nurse, LaboratoryStaff | — | Returns patient + full medical history; audited PHI read |
| `PUT` | `/api/patients/{id}` | Doctor, Nurse | `UpdatePatientDto` | Full replace including allergy set; audited |
| `POST` | `/api/cds/evaluate` | Doctor, Nurse | `CdsEvaluationRequestDto` | Optional `PatientId`; merges stored + explicit inputs |
| `POST` | `/api/laboratory/order` | Doctor, Nurse | `OrderLabTestDto` | Requires existing `RecordId`; returns `LabResultDto` |
| `PUT` | `/api/laboratory/results/{id}` | LaboratoryStaff | `UpdateLabResultDto` | Auto-classifies status (Normal/High/Low/Indeterminate) |
| `GET` | `/api/laboratory/patient/{patientId}` | Doctor, Nurse, LaboratoryStaff | — | Audited PHI read |
| `POST` | `/api/medications` | Doctor | `CreateMedicationDto` | Safety gate: `409 Conflict` on Critical drug conflict |
| `PUT` | `/api/medications/{id}/discontinue` | Doctor | — | Idempotent; `Active → Discontinued` only |
| `POST` | `/api/appointments` | Doctor, Nurse, Administrator | `CreateAppointmentDto` | `AppointmentDate` must be in the future |
| `GET` | `/api/appointments/doctor/{doctorId}` | Doctor, Nurse, Administrator | — | Ordered by `AppointmentDate` ascending |
| `GET` | `/api/reports/monthly-stats` | Administrator | — | `?month&year`; month must be 1–12 |
| `GET` | `/api/audit` | Administrator | — | Paginated; `?page&pageSize` |

### Sample requests

**Login:**
```bash
curl -X POST https://localhost:{port}/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"admin","password":"Admin@12345"}'
```

**Register a Doctor:**
```bash
curl -X POST https://localhost:{port}/api/auth/register \
  -H "Authorization: Bearer <token>" \
  -H "Content-Type: application/json" \
  -d '{"username":"dr.smith","password":"Secure@Pass1","fullName":"Dr Jane Smith","email":"jane@hospital.local","role":"Doctor","department":"General"}'
```

**Register a patient:**
```bash
curl -X POST https://localhost:{port}/api/patients \
  -H "Authorization: Bearer <token>" \
  -H "Content-Type: application/json" \
  -d '{"firstName":"John","surname":"Doe","gender":"Male","dateOfBirth":"1985-03-15","address":"123 Main St","phone":"0821234567","bloodType":"APositive","allergies":["Penicillin"]}'
```

**Create a medical record (required before lab/medication):**
```bash
curl -X POST https://localhost:{port}/api/medical-records \
  -H "Authorization: Bearer <token>" \
  -H "Content-Type: application/json" \
  -d '{"patientId":1,"doctorId":2,"diagnosis":"Suspected Diabetes Mellitus","visitDate":"2025-01-15T09:00:00Z"}'
```

**Order a lab test:**
```bash
curl -X POST https://localhost:{port}/api/laboratory/order \
  -H "Authorization: Bearer <token>" \
  -H "Content-Type: application/json" \
  -d '{"recordId":1,"testName":"Glucose","referenceRange":"70-110 mg/dL"}'
```

**Upload lab result:**
```bash
curl -X PUT https://localhost:{port}/api/laboratory/results/1 \
  -H "Authorization: Bearer <token>" \
  -H "Content-Type: application/json" \
  -d '{"resultValue":"250"}'
```

**CDS evaluation (with PatientId to merge stored data):**
```bash
curl -X POST https://localhost:{port}/api/cds/evaluate \
  -H "Authorization: Bearer <token>" \
  -H "Content-Type: application/json" \
  -d '{"patientId":1,"symptoms":["polyuria","polydipsia","fatigue"]}'
```

**Prescribe a medication:**
```bash
curl -X POST https://localhost:{port}/api/medications \
  -H "Authorization: Bearer <token>" \
  -H "Content-Type: application/json" \
  -d '{"recordId":1,"drugName":"Metformin","dosage":"500mg","frequency":"Twice daily"}'
```

**Book an appointment:**
```bash
curl -X POST https://localhost:{port}/api/appointments \
  -H "Authorization: Bearer <token>" \
  -H "Content-Type: application/json" \
  -d '{"patientId":1,"doctorId":2,"appointmentDate":"2025-03-01T10:00:00Z"}'
```

**Monthly stats report:**
```bash
curl "https://localhost:{port}/api/reports/monthly-stats?month=1&year=2025" \
  -H "Authorization: Bearer <token>"
```

---

## 10. Project Structure

```
DCDSS/                              — project root (net8.0, DCDSS namespace)
  Controllers/                      — HTTP endpoints; routing, auth attributes, DTO mapping
  Services/                         — business logic implementations
  Services/Interfaces/              — service + repo abstractions (Core layer: IRepository<T>, IUnitOfWork)
  Repositories/                     — EF Core repository implementations (Repository<T>, UnitOfWork, specific repos)
  Repositories/Interfaces/          — specific repository contracts (IUserRepository, IPatientRepository, etc.)
  Entities/                         — EF Core entity POCOs (plain C# classes, no logic)
  Entities/Enums/                   — enum types stored as strings in the database
  DTOs/                             — request/response shapes grouped by module
  DTOs/Common/                      — PagedResult<T>, IAuditableResult (audit filter hook)
  DTOs/Auth/                        — RegisterDto, LoginDto, UserDto, LoginResponseDto
  DTOs/MedicalRecords/              — CreateMedicalRecordDto, MedicalRecordDto
  DTOs/Patients/                    — CreatePatientDto, UpdatePatientDto, PatientSummaryDto, PatientDetailDto
  DTOs/Cds/                         — CdsEvaluationRequestDto, CdsEvaluationResponseDto, CdsFindingDto, etc.
  DTOs/Laboratory/                  — OrderLabTestDto, UpdateLabResultDto, LabResultDto
  DTOs/Medications/                 — CreateMedicationDto, MedicationDto, PrescribeMedicationResponseDto, SafetyWarningDto
  DTOs/Appointments/                — CreateAppointmentDto, AppointmentDto
  DTOs/Reports/                     — MonthlyStatsDto, DepartmentStatDto
  DTOs/Audit/                       — AuditLogDto
  Models/                           — non-entity domain models
  Models/Cds/                       — CDS rule types, finding models, LabResultSelector, SafetyConflict, SafetyCheckResult
  Data/                             — DcdssContext (DbContextFactory)
  Data/Configurations/              — IEntityTypeConfiguration<T> per entity (Fluent API only)
  Middleware/                       — GlobalExceptionMiddleware, AuditActionFilter, AuditActionAttribute
  Security/                         — CurrentUserService (reads actor from JWT claims via IHttpContextAccessor)
  Configuration/                    — typed IOptions<T> settings: JwtSettings, CorsSettings, AdminSeedSettings, JwtSettingsValidator
  Exceptions/                       — NotFoundException, ConflictException, BusinessRuleException
  Migrations/                       — EF Core generated migration files
```

**Layering rules:**
- **API layer** (`Controllers/`, `Middleware/`, `Program.cs`) — HTTP concerns only; no DbContext access, no business logic.
- **Core/Business layer** (`Services/`, `DTOs/`, `Models/`) — use-case orchestration, business rules, entity↔DTO mapping; depends only on repository interfaces, never on DbContext directly.
- **Data layer** (`Data/`, `Repositories/`, `Entities/`) — persistence; EF Core entities, Fluent API config, repository implementations.
- Dependency direction: **API → Core → Data**. Data never references API.

---

## 11. Assumptions & Known Limitations

**PatientAllergy normalized table**
Allergies are stored as one row per allergen per patient in a `PatientAllergy` child table (not a delimited string column) for 3NF compliance. CDS and safety checks load allergens as rows — no comma parsing.

**AuditLog.UserId is nullable**
The audit log must also capture unauthenticated events (e.g. a failed login attempt with an unknown username has no valid `UserId`). When the actor is unknown, `UserId` is `null` and the attempted identifier is recorded in `Details`.

**LabResultStatus.Indeterminate for threshold-form result values**
A result value reported as `"<5"` or `">200"` (a threshold form) is classified `Indeterminate` — the exact numeric value is not known, so the bound is not compared as if it were the measured value. CDS critical-lab rules still use the numeric bound for threshold comparison (a `"<2"` glucose fires the critical-low alert at `CriticalLow=2.5`), but `LabResult.Status` records `Indeterminate` to avoid false High/Low alerts.

**MedicalRecordStatus lifecycle**
The original schema did not specify status values for `MedicalRecords`. The assumed lifecycle is: `Active`, `Resolved`, `Archived`.

**No DELETE endpoints**
Lifecycle is managed via status fields (`MedicationStatus`, `AppointmentStatus`, `MedicalRecordStatus`). All FK relationships use `OnDelete Restrict` — deleting a parent row with existing children is blocked at the database level.

**UTC storage — SAST timezone shift**
All `DateTime` values are stored and compared as UTC. Report `month`/`year` boundaries and appointment display times reflect UTC, not SAST (UTC+2). A SAST calendar day or month can split across two UTC periods, so displayed times will read 2 hours behind SAST wall-clock time. Time-zone-aware storage is a backlog item.

**Drug and allergen names must match canonical seed names**
CDS matching uses case-insensitive exact comparison against the seed rule names (e.g. `"Warfarin"`, `"Penicillin"`, `"Sulfa"`). Free-text drug and allergen names entered by clinicians must match these canonical names for rules to fire. A controlled drug/allergen catalog is a future item.

**No forced-password-change flow**
There is no `MustChangePassword` flag or change-password endpoint in this version. The seeded/generated administrator password is a bootstrap credential and must be rotated manually. The recommended path is to register a new `Administrator` account and stop using the seeded account, or override `AdminSeed__Password` via environment variable.

**Units are not auto-converted**
Lab result classification proceeds numerically on the raw `ResultValue` and `ReferenceRange` strings without unit conversion (e.g. `mg/dL` vs `mmol/L`). A warning is logged when the units in the result value and reference range appear to differ. Unit-aware conversion is a backlog item.
