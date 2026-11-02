# DCDSS Technical Design (Implementation-Ready)

Target: `Digital Clinical Decision Support System (DCDSS) for Rural South African Hospitals` — .NET 8 Web API backend. This document is the single source of truth for the planner and coder. Every decision is final; there are no open questions for the implementer to resolve.

This is **revision 2**. It incorporates every finding from `.agents/tasks/design-review.md` (verdict CHANGES_REQUESTED: 1 HIGH, 8 MEDIUM, 10 NIT). Changes are woven into the body; a per-finding response table is at the end (Section 17).

Workspace root (abbreviated as `$ROOT` below) =
`c:/Users/nm/IdeaProjects/Digital Clinical Decision Support System (DCDSS)`.
Project root (abbreviated as `$PROJ` below) = `$ROOT/DCDSS`.

---

## 1. Architecture Overview

A **single-project modular monolith** (`$PROJ`, SDK `Microsoft.NET.Sdk.Web`, target `net8.0`) with **strict layering** enforced by folder/namespace boundaries. No multi-project split; layering is a convention, not a compiler gate, but it is mandatory and the file-by-file outline (Section 15) follows it exactly.

Three layers:
- **API layer** — `Controllers/`, `Program.cs`, `Middleware/`. HTTP concerns only: routing, authorization attributes, model binding, mapping DTO→service calls, shaping HTTP responses. No business logic, no DbContext access.
- **Core/Business layer** — `Services/` (+ `Services/Interfaces/`), `DTOs/`, `Models/`. Orchestrates use cases, enforces business rules/invariants, maps entities↔DTOs. Depends on repository interfaces, never on DbContext directly. **Holds only abstractions that the Data layer implements** (`IRepository<T>`, `IUnitOfWork` live here; their implementations live in Data).
- **Data layer** — `Data/` (DbContext + `Data/Configurations/`), `Repositories/` (+ `Repositories/Interfaces/`, and the **generic `Repository<T>` + `UnitOfWork` implementations**), `Entities/`. Owns persistence: EF Core entities, Fluent API config, repositories, DbContext.

Cross-cutting:
- `Security/` — `ICurrentUserService` (reads the actor from JWT claims).
- `Configuration/` — typed `IOptions<T>` settings classes + options validators.
- `Exceptions/` — domain exception types consumed by the exception middleware.
- `Models/` — non-entity domain models (CDS rule models, reference-range parse result).

Root namespace `DCDSS`; sub-namespaces aligned to folders (e.g., `DCDSS.Controllers`, `DCDSS.Services.Interfaces`, `DCDSS.Data.Configurations`, `DCDSS.Repositories`). PascalCase public members, `_camelCase` private fields, async/await throughout data access, `ActionResult<T>` return types, constructor DI everywhere.

Dependency direction (enforced by convention): API → Core → Data. **Data never references API; Data references Core only for the repository/UoW abstractions it implements (`IRepository<T>`, `IUnitOfWork`) and the entity/DTO/models types it shares** — this is a clean, downward dependency (abstraction in Core, implementation in Data), not a tangle. Core never references API. Entities are plain POCOs (no logic), so Core and Data both depend on them.

**Layering correction (vs. revision 1):** the generic `Repository<T>` and `UnitOfWork` *implementations* now live in `Repositories/` (Data), while `IRepository<T>` and `IUnitOfWork` stay in `Services/Interfaces/` (Core). This keeps the dependency direction consistent (Core defines abstractions, Data implements them; concrete Data repos derive from a Data-layer base).

**Folders added beyond the scaffold** (the scaffold list was illustrative; these additions are intentional):
- `DCDSS/Exceptions/` — domain exception types.
- `DCDSS/Data/Configurations/` — one `IEntityTypeConfiguration<T>` per entity.
- `DCDSS/Models/Cds/` — CDS rule + finding models.
- `DCDSS/DTOs/<Module>/` — DTOs grouped per module; `DTOs/Common/` for shared envelopes + the `IAuditableResult` marker (Section 9).
- `DCDSS/DTOs/MedicalRecords/` — new module (Section 5.2).

---

## 2. Primary Key Strategy

**Decision: `int` auto-increment identity for every entity, including the new `PatientAllergy` join-like entity.**

Justification:
- Bounded scale (rural hospitals, single-tenant, no offline sync/merge requirement) — there is no need for GUID's distributed-generation advantage.
- Sequential `int` IDs keep indexes compact (no GUID fragmentation), are cheaper for MySQL/Pomelo, and produce readable URLs/logs.
- The schema's column names are `UserID`, `PatientID`, etc. — mapped to C# properties `UserId`, `PatientId`, etc. (PascalCase), with the original column names restored via Fluent API `HasColumnName` (see Section 3 mapping tables).

Configuration: `.HasKey(e => e.UserId)` + `.Property(e => e.UserId).ValueGeneratedOnAdd()` (Pomelo maps `int` with `ValueGeneratedOnAdd` to MySQL `AUTO_INCREMENT`; explicit for clarity). Applied identically on all 8 entities (the original 7 plus `PatientAllergy`).

---

## 3. Entity Design

Enums are stored as **strings** (not ints) for readability and forward compatibility. Each enum is a C# `enum` in `Entities/Enums/`; entities expose the enum-typed property; Fluent API configures `.HasConversion<string>()` and `HasMaxLength`. Closed-set fields use enums; free text uses `string`.

Enum definitions:
- `UserRole { Doctor, Nurse, LaboratoryStaff, Administrator }`
- `Gender { Male, Female, Other }` *(assumption: schema only said "Gender"; this set is inclusive and standard for clinical systems)*
- `BloodType { APositive, ANegative, BPositive, BNegative, ABPositive, ABNegative, OPositive, ONegative }`
- `MedicalRecordStatus { Active, Resolved, Archived }` *(schema gave no values; "Active/Resolved/Archived" is the assumed lifecycle)*
- `LabResultStatus { Ordered, Normal, High, Low, Indeterminate }` — see Section 7 for member semantics (threshold-form values classify as `Indeterminate`).
- `MedicationStatus { Active, Discontinued }`
- `AppointmentStatus { Scheduled, Completed, Cancelled }`

Entities (C# property name → schema column). All `CreatedAt`/date defaults use **UTC** (`DateTime.UtcNow`); `DateTime` columns are stored as MySQL `DATETIME` and treated as UTC. **Known limitation (Section 14):** because everything is UTC, a SAST (UTC+2) calendar day/month can split across two UTC periods, and displayed appointment times read 2 hours behind SAST. This is documented, not silently assumed.

### User
| Property | C# type | Column | Constraints |
|---|---|---|---|
| UserId | int | UserID | PK, identity |
| Username | string | Username | Required, MaxLength 50, unique, indexed |
| PasswordHash | string | PasswordHash | Required, **MaxLength 512** (PBKDF2 output headroom across parameter changes) |
| FullName | string | FullName | Required, MaxLength 100 |
| Email | string | Email | Required, MaxLength 150, unique, indexed, email format |
| Role | UserRole | Role | Required, enum→string, MaxLength 20 |
| Department | string? | Department | Optional, MaxLength 100 |
| CreatedAt | DateTime | CreatedAt | Required, default UTC now |

Navigations: `ICollection<MedicalRecord> AuthoredMedicalRecords` (DoctorId), `ICollection<LabResult> OrderedLabResults` (OrderedBy), `ICollection<Appointment> Appointments` (DoctorId), `ICollection<AuditLog> AuditLogs` (UserId). Department is optional because Administrators/Lab staff are not clinically departmental.

### Patient
| Property | C# type | Column | Constraints |
|---|---|---|---|
| PatientId | int | PatientID | PK, identity |
| FirstName | string | FirstName | Required, MaxLength 50 |
| Surname | string | Surname | Required, MaxLength 50 |
| Gender | Gender | Gender | Required, enum→string, MaxLength 10 |
| DateOfBirth | DateOnly | DOB | Required (MySQL DATE) |
| Address | string | Address | Required, MaxLength 200 |
| Phone | string | Phone | Required, MaxLength 20 |
| BloodType | BloodType | BloodType | Required, enum→string, MaxLength 12 |
| EmergencyContact | string? | EmergencyContact | Optional, MaxLength 100 |
| CreatedAt | DateTime | CreatedAt | Required, default UTC now |

**No `Allergies` string column.** Allergies are now a normalized child table (`PatientAllergy`, below) — this preserves the brief's "at least 3NF" requirement (revision 1 stored a comma-delimited list in one column, a 1NF/3NF violation; that is corrected here). `DateOnly` is used for DOB (birth date has no time component). Pomelo 8.x maps `DateOnly` to MySQL `DATE`. Navigations: `ICollection<MedicalRecord> MedicalRecords`, `ICollection<Appointment> Appointments`, `ICollection<PatientAllergy> Allergies`. Lab results are reached via MedicalRecords.

### PatientAllergy (NEW — normalized allergy storage)
| Property | C# type | Column | Constraints |
|---|---|---|---|
| PatientAllergyId | int | PatientAllergyID | PK, identity |
| PatientId | int | PatientID | Required, FK→Patients, OnDelete Restrict, indexed |
| Allergen | string | Allergen | Required, MaxLength 100 |

Unique index on `(PatientId, Allergen)`. Navigation: `Patient Patient`. This is the 8th table; it is a deliberate schema extension to support the allergy-safety feature while staying 3NF (one row per allergen per patient, no repeating groups). The CDS / safety logic loads allergens as rows (no comma parsing).

### MedicalRecord
| Property | C# type | Column | Constraints |
|---|---|---|---|
| RecordId | int | RecordID | PK, identity |
| PatientId | int | PatientID | Required, FK→Patients, indexed |
| DoctorId | int | DoctorID | Required, FK→Users, indexed |
| Diagnosis | string | Diagnosis | Required, MaxLength 500 |
| TreatmentNotes | string? | TreatmentNotes | Optional, MaxLength 2000 |
| VisitDate | DateTime | VisitDate | Required, indexed |
| Status | MedicalRecordStatus | Status | Required, enum→string, MaxLength 20, default Active |

Navigations: `Patient Patient`, `User Doctor`, `ICollection<LabResult> LabResults`, `ICollection<Medication> Medications`.

### LabResult
| Property | C# type | Column | Constraints |
|---|---|---|---|
| ResultId | int | ResultID | PK, identity |
| RecordId | int | RecordID | Required, FK→MedicalRecords, indexed |
| TestName | string | TestName | Required, MaxLength 100 |
| ResultValue | string? | ResultValue | Optional (null until results uploaded), MaxLength 50 |
| ReferenceRange | string | ReferenceRange | Required, MaxLength 100 |
| Status | LabResultStatus | Status | Required, enum→string, MaxLength 20, default Ordered |
| OrderedBy | int | OrderedBy | Required, FK→Users, indexed |
| DateOrdered | DateTime | DateOrdered | Required, indexed, default UTC now |

`ResultValue` is a string (preserves `"70"`, `"<5"`, `">200"`, and qualitative values). Classification parses it numerically (Section 7). Navigations: `MedicalRecord Record`, `User OrderedByUser`.

### Medication
| Property | C# type | Column | Constraints |
|---|---|---|---|
| MedicationId | int | MedicationID | PK, identity |
| RecordId | int | RecordID | Required, FK→MedicalRecords, indexed |
| DrugName | string | DrugName | Required, MaxLength 100 |
| Dosage | string | Dosage | Required, MaxLength 50 |
| Frequency | string | Frequency | Required, MaxLength 50 |
| StartDate | DateTime | StartDate | Required, default UTC now |
| Status | MedicationStatus | Status | Required, enum→string, MaxLength 20, default Active |

No direct PatientId — a medication belongs to a MedicalRecord, which belongs to a Patient. The safety check resolves the patient via `MedicalRecord.PatientId`. Navigations: `MedicalRecord Record`.

### Appointment
| Property | C# type | Column | Constraints |
|---|---|---|---|
| AppointmentId | int | AppointmentID | PK, identity |
| PatientId | int | PatientID | Required, FK→Patients, indexed |
| DoctorId | int | DoctorID | Required, FK→Users, indexed |
| AppointmentDate | DateTime | AppointmentDate | Required, indexed (date + time-of-day — `DateTime`, not date-only) |
| Status | AppointmentStatus | Status | Required, enum→string, MaxLength 20, default Scheduled |

Navigations: `Patient Patient`, `User Doctor`.

### AuditLog
| Property | C# type | Column | Constraints |
|---|---|---|---|
| AuditId | int | AuditID | PK, identity |
| UserId | int? | UserID | Optional, FK→Users (Restrict), indexed |
| Action | string | Action | Required, MaxLength 50 |
| Details | string? | Details | Optional, MaxLength 1000 |
| Timestamp | DateTime | Timestamp | Required, indexed, default UTC now |

**`UserId` is nullable** (documented deviation). The schema said `UserID (FK→Users)`, but audit must also record unauthenticated events (e.g. a failed login with an unknown username has no valid user). When the actor is unknown/unauthenticated, `UserId` is null and the attempted identifier is written to `Details`. Navigation: `User? User`.

### Configuration approach
**Decision: `IEntityTypeConfiguration<T>` files**, one per entity in `Data/Configurations/`, applied via `modelBuilder.ApplyConfigurationsFromAssembly(typeof(DcdssContext).Assembly)` in `OnModelCreating`. Keeps `DcdssContext` lean, configs co-located, easy to read. Data annotations are used **only for DTO validation** (input), not for entity mapping; all entity mapping is Fluent API. One configuration file per entity (8 files incl. `PatientAllergyConfiguration`).

---

## 4. Relationships, Cascade Rules, Indexes

**Cascade policy: every FK relationship uses `Restrict` (no cascade).** Deleting a parent with existing children is blocked by the database. Clinical data is never silently removed; lifecycle is managed via status fields (e.g. `MedicationStatus`, `AppointmentStatus`, `MedicalRecordStatus`), not deletion. There are **no DELETE endpoints**, so this policy is never exercised by the API but protects the DB against direct edits.

| From (1) | To (many) | FK property | OnDelete |
|---|---|---|---|
| Patient | MedicalRecord | MedicalRecord.PatientId | Restrict |
| Patient | Appointment | Appointment.PatientId | Restrict |
| **Patient** | **PatientAllergy** | **PatientAllergy.PatientId** | **Restrict** |
| User (Doctor) | MedicalRecord | MedicalRecord.DoctorId | Restrict |
| User | LabResult | LabResult.OrderedBy | Restrict |
| User (Doctor) | Appointment | Appointment.DoctorId | Restrict |
| User | AuditLog | AuditLog.UserId (nullable) | Restrict |
| MedicalRecord | LabResult | LabResult.RecordId | Restrict |
| MedicalRecord | Medication | Medication.RecordId | Restrict |

Indexes (explicit, beyond the FK-convention indexes):
- `Users.Username` — unique
- `Users.Email` — unique
- `MedicalRecords.PatientId`, `MedicalRecords.DoctorId`, `MedicalRecords.VisitDate`
- `LabResults.RecordId`, `LabResults.OrderedBy`, `LabResults.DateOrdered`
- `Medications.RecordId`
- `Appointments.PatientId`, `Appointments.DoctorId`, `Appointments.AppointmentDate`
- `AuditLogs.UserId`, `AuditLogs.Timestamp`
- `PatientAllergies.PatientId` — index; `PatientAllergies (PatientId, Allergen)` — unique composite index

---

## 5. Per-Module Design

Generic repository: `IRepository<T> where T : class` (Core abstraction, `Services/Interfaces/`) with `GetByIdAsync`, `GetAllAsync`, `AddAsync`, `UpdateAsync`, `RemoveAsync`, and `IQueryable<T> Query()` (lets services compose `Where`/`OrderBy`/`Include`). `Repository<T>` (Data, `Repositories/`) is the base implementing against `DcdssContext`. `IUnitOfWork.SaveChangesAsync()` commits; services add via repos then save via UoW. Specific repositories add bespoke queries (e.g. `IUserRepository.GetByUsernameAsync`).

Every controller is `[ApiController]`, uses an **explicit route** (not `api/[controller]`) so the public paths match the spec exactly, and uses `ActionResult<T>`/`IActionResult`. The audit filter (Section 9) auto-logs every action decorated with `[AuditAction]` (mutating **and** PHI-read actions).

**Authorization default:** a global fallback policy requires an authenticated user (`AddAuthorization(o => o.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())`). Login is `[AllowAnonymous]`; every other action carries an explicit `[Authorize(Roles=...)]`. The fallback is a safety net so a forgotten role attribute cannot expose an endpoint anonymously.

### RBAC matrix
Roles: Doctor (D), Nurse (N), LaboratoryStaff (L), Administrator (A). "—" = no access. (Module order below: Auth, Medical Records, Patients, CDS, Laboratory, Medications, Appointments, Reporting & Audit.)

| Endpoint | D | N | L | A | Audit action |
|---|---|---|---|---|---|
| POST /api/auth/register | — | — | — | ✅ | REGISTER |
| POST /api/auth/login | anon | anon | anon | anon | LOGIN / LOGIN_FAILED |
| POST /api/medical-records | ✅ | ✅ | — | — | CREATE_MEDICAL_RECORD |
| GET /api/patients | ✅ | ✅ | ✅ | — | (not audited — list) |
| POST /api/patients | ✅ | ✅ | — | — | CREATE_PATIENT |
| GET /api/patients/{id} | ✅ | ✅ | ✅ | — | VIEW_PATIENT |
| PUT /api/patients/{id} | ✅ | ✅ | — | — | UPDATE_PATIENT |
| POST /api/cds/evaluate | ✅ | ✅ | — | — | CDS_EVALUATE |
| POST /api/laboratory/order | ✅ | ✅ | — | — | CREATE_LAB_ORDER |
| PUT /api/laboratory/results/{id} | — | — | ✅ | — | UPDATE_LAB_RESULT |
| GET /api/laboratory/patient/{patientId} | ✅ | ✅ | ✅ | — | VIEW_LAB_HISTORY |
| POST /api/medications | ✅ | — | — | — | CREATE_MEDICATION |
| PUT /api/medications/{id}/discontinue | ✅ | — | — | — | DISCONTINUE_MEDICATION |
| POST /api/appointments | ✅ | ✅ | — | ✅ | CREATE_APPOINTMENT |
| GET /api/appointments/doctor/{doctorId} | ✅ | ✅ | — | ✅ | (not audited — list) |
| GET /api/reports/monthly-stats | — | — | — | ✅ | (not audited) |
| GET /api/audit | — | — | — | ✅ | (not audited) |

Rationale notes: patient/lab-history reads include LaboratoryStaff because lab staff need clinical context to upload/interpret results; prescribing is Doctor-only to reflect South African prescribing norms (nurses have limited/no independent prescribing for scheduled drugs in this system); appointment booking/read is clinical + administrative (reception-style). **PHI read access (`GET /api/patients/{id}`, `GET /api/laboratory/patient/{patientId}`) IS audited** (POPIA treats access to personal/health information as a sensitive action); list/report/audit reads are not audited to avoid noise and because they do not target an individual's record.

### 5.1 Auth — `AuthController` (`[Route("api/auth")]`)
- `POST register` — `[Authorize(Roles="Administrator")]`. Request `RegisterDto`; calls `IAuthService.RegisterAsync`; returns `200 OK` with `UserDto` (no GET-by-id for users, so `200` rather than `CreatedAtAction`). Audit `REGISTER`.
- `POST login` — `[AllowAnonymous]`. Request `LoginDto`; calls `IAuthService.LoginAsync`; returns `200 OK` with `LoginResponseDto`. Audit `LOGIN` (success) / `LOGIN_FAILED` (failure). On any failure → `401` with generic `"Invalid username or password."`.

**`IAuthService`**: `RegisterAsync(RegisterDto)` → validate unique username+email (throw `ConflictException` if dup), hash with `PasswordHasher<User>`, create `User`, save, return `UserDto`. `LoginAsync(LoginDto)` → fetch user by username; verify password via `PasswordHasher<User>.VerifyHashedPassword`; on success generate token via `ITokenService` and return `LoginResponseDto`; on failure (user missing OR hash mismatch) throw `UnauthorizedAccessException` (mapped to 401). **Never reveal which of username/password was wrong** — both missing-user and wrong-password produce the same 401 message; only the audit log records the attempted username (in `Details`, an identifier — not PII).

DTOs (`DTOs/Auth/`):
- `RegisterDto`: `Username` [Required, MaxLength 50], `Password` [Required, MinLength 8, MaxLength 100], `FullName` [Required, MaxLength 100], `Email` [Required, EmailAddress, MaxLength 150], `Role` [Required, EnumDataType(typeof(UserRole))], `Department` [MaxLength 100, optional].
- `LoginDto`: `Username` [Required, MaxLength 50], `Password` [Required].
- `UserDto`: `UserId, Username, FullName, Email, Role, Department, CreatedAt`. No `PasswordHash`. Implements `IAuditableResult.EntityId => UserId`.
- `LoginResponseDto`: `Token, Username, Role (string), ExpiresAt (DateTime)`.

Repository: `IUserRepository.GetByUsernameAsync`, `GetByEmailAsync`, `AddAsync`. Uniqueness is checked in service *and* backed by unique DB indexes (the index is the real invariant owner — service check gives a friendly 409; index prevents races).

### 5.2 Medical Records (NEW) — `MedicalRecordsController` (`[Route("api/medical-records")]`)
This module exists to **unblock the Laboratory and Medication modules**: `LabResult.RecordId` and `Medication.RecordId` are required FKs to `MedicalRecords`, so a record must exist before a test can be ordered or a drug prescribed. Without this endpoint those two modules are infeasible.

- `POST ""` — `[Authorize(Roles="Doctor,Nurse")]`. Request `CreateMedicalRecordDto`; calls `IMedicalRecordService.CreateAsync`; returns `201 Created` with `Location: /api/medical-records/{RecordId}` and `MedicalRecordDto` body (`Created($"/api/medical-records/{dto.RecordId}", dto)` — no unlisted GET-by-id endpoint is added). Audit `CREATE_MEDICAL_RECORD`.

**`IMedicalRecordService.CreateAsync(CreateMedicalRecordDto)`** validates: `PatientId` exists (else `NotFoundException` → 404), `DoctorId` exists and `Role==Doctor` (else `BusinessRuleException` → 400 "Selected user is not a Doctor."), `VisitDate` not in the future (else `BusinessRuleException` → 400); creates `MedicalRecord` with `Status=Active`; save via UoW; returns mapped `MedicalRecordDto`.

DTOs (`DTOs/MedicalRecords/`):
- `CreateMedicalRecordDto`: `PatientId` [Required], `DoctorId` [Required], `Diagnosis` [Required, MaxLength 500], `TreatmentNotes` [MaxLength 2000, optional], `VisitDate` [Required] (DateTime). *(Future-date and doctor-role checks are service-enforced `BusinessRuleException`s → 400; there is no built-in `[Future]` data annotation.)*
- `MedicalRecordDto`: `RecordId, PatientId, DoctorId, DoctorName, Diagnosis, TreatmentNotes, VisitDate, Status, LabResults: IList<LabResultDto>, Medications: IList<MedicationDto>`. Implements `IAuditableResult.EntityId => RecordId`.

Repository: `IMedicalRecordRepository.GetByIdAsync(int id)` (used by lab/medication services to resolve a record and its `PatientId`). The controller calls the service; the service uses `IRepository<Patient>`/`IRepository<User>` for existence checks and `IMedicalRecordRepository` to add.

### 5.3 Patients — `PatientsController` (`[Route("api/patients")]`)
- `GET ""` — `[Authorize(Roles="Doctor,Nurse,LaboratoryStaff")]`. Query `page` (int, default 1), `pageSize` (int, default 20), `search` (string?, name match on FirstName/Surname, case-insensitive `Contains`). Returns `200 OK` with `PagedResult<PatientSummaryDto>`.
- `POST ""` — `[Authorize(Roles="Doctor,Nurse")]`. Request `CreatePatientDto`; returns `201 Created` with `Location: /api/patients/{PatientId}` and `PatientDetailDto`. Audit `CREATE_PATIENT`.
- `GET "{id}"` — `[Authorize(Roles="Doctor,Nurse,LaboratoryStaff")]`. Returns `200 OK` with `PatientDetailDto` (patient + medical history). `404` if not found. Audit `VIEW_PATIENT` (PHI read).
- `PUT "{id}"` — `[Authorize(Roles="Doctor,Nurse")]`. Request `UpdatePatientDto` (full replace, including allergy set). `404` if not found; returns `200 OK` with `PatientDetailDto`. Audit `UPDATE_PATIENT`.

**`IPatientService`**: `GetPagedAsync(page, pageSize, search)`, `GetByIdWithHistoryAsync(id)` (include `MedicalRecords.LabResults` + `MedicalRecords.Medications` + `MedicalRecords.Doctor` + `Allergies`), `CreateAsync(CreatePatientDto)` (maps to entity, validates DOB not in future — throw `BusinessRuleException` if DOB > today; then creates `PatientAllergy` rows for each allergen — trim, drop empties, dedup case-insensitively), `UpdateAsync(id, UpdatePatientDto)` (throw `NotFoundException` if missing; replace patient fields and **replace the allergy set** — delete existing `PatientAllergy` rows for the patient, insert the new normalized set). DOB invariant (not in future) and allergy-set replacement are owned by the service.

**Pagination (unified rule, applies to patients, audit, and any future paged endpoint):** the service clamps `page = max(1, page)` and `pageSize = clamp(pageSize, 1, 100)`. There is no 400 for out-of-range page/pageSize — they are clamped. A 400 happens only when the query string cannot bind to `int` (handled by `[ApiController]` model binding → `ValidationProblemDetails`), which is non-clampable. (Reports `month`/`year` are *not* pagination — see 5.8 — and are validated with a 400 if out of range.)

DTOs (`DTOs/Patients/`):
- `CreatePatientDto`: `FirstName, Surname` [Required, MaxLength 50]; `Gender` [Required, EnumDataType]; `DateOfBirth` [Required] (DateOnly); `Address` [Required, MaxLength 200]; `Phone` [Required, MaxLength 20, regex `^[0-9+()\-\s]{7,20}$`]; `BloodType` [Required, EnumDataType]; `EmergencyContact` [MaxLength 100, optional]; `Allergies` — `IList<string>?` optional list of allergen names (no length annotation on the list; each entry is trimmed by the service, entries >100 chars are rejected by the DB MaxLength).
- `UpdatePatientDto`: same fields/validations as `CreatePatientDto` (PUT = full replace, including `Allergies`).
- `PatientSummaryDto`: `PatientId, FirstName, Surname, Gender, DateOfBirth, Phone, BloodType, CreatedAt`.
- `PatientDetailDto`: all Patient fields + `IList<string> Allergies` (mapped from `PatientAllergy` rows) + `IList<MedicalRecordDto> MedicalHistory`. Implements `IAuditableResult.EntityId => PatientId`.

Repository: `IPatientRepository.SearchAsync(page, pageSize, search)` (returns `(items, total)`), `GetByIdWithHistoryAsync(id)` (one query with includes). Allergy operations: `IPatientAllergyRepository.GetByPatientAsync(patientId)`, `DeleteByPatientAsync(patientId)`, `AddAsync(PatientAllergy)`.

### 5.4 CDS — `CdsController` (`[Route("api/cds")]`)
- `POST evaluate` — `[Authorize(Roles="Doctor,Nurse")]`. Request `CdsEvaluationRequestDto`; returns `200 OK` with `CdsEvaluationResponseDto`. Audit `CDS_EVALUATE`.

See Section 6 for the engine design.

### 5.5 Laboratory — `LaboratoryController` (`[Route("api/laboratory")]`)
- `POST order` — `[Authorize(Roles="Doctor,Nurse")]`. Request `OrderLabTestDto`. Creates `LabResult` with `Status=Ordered`, `OrderedBy` = current user id, `DateOrdered` = now. `404` if `RecordId` not found. Returns `201 Created` with `Location: /api/laboratory/results/{ResultId}` and `LabResultDto`. Audit `CREATE_LAB_ORDER`.
- `PUT results/{id}` — `[Authorize(Roles="LaboratoryStaff")]`. Request `UpdateLabResultDto`. Sets `ResultValue` (and optional `ReferenceRange` override), computes `Status` via `IReferenceRangeParser`. `404` if not found. Returns `200 OK` with `LabResultDto`. Audit `UPDATE_LAB_RESULT`.
- `GET patient/{patientId}` — `[Authorize(Roles="Doctor,Nurse,LaboratoryStaff")]`. Returns `200 OK` with `IList<LabResultDto>` (all lab results across the patient's medical records). Empty list if none. Audit `VIEW_LAB_HISTORY` (PHI read).

**`ILabService`**: `OrderAsync(OrderLabTestDto, int orderedByUserId)`, `UpdateResultAsync(int id, UpdateLabResultDto)`, `GetByPatientAsync(int patientId)`. The classification invariant (Status is only set on result upload, by this service) is owned by `ILabService`. Lab-history retrieval uses the same `GetByPatientAsync`.

DTOs (`DTOs/Laboratory/`):
- `OrderLabTestDto`: `RecordId` [Required], `TestName` [Required, MaxLength 100], `ReferenceRange` [Required, MaxLength 100] (e.g. `"70-110 mg/dL"`).
- `UpdateLabResultDto`: `ResultValue` [Required, MaxLength 50], `ReferenceRange` [MaxLength 100, optional].
- `LabResultDto`: `ResultId, RecordId, TestName, ResultValue, ReferenceRange, Status, OrderedBy, DateOrdered`. Implements `IAuditableResult.EntityId => ResultId`.

Repository: `ILabResultRepository.GetByPatientAsync(patientId)` (join through MedicalRecords), `GetByIdAsync(id)`, `AddAsync`.

### 5.6 Medications — `MedicationsController` (`[Route("api/medications")]`)
- `POST ""` — `[Authorize(Roles="Doctor")]`. Request `CreateMedicationDto`. Resolves `PatientId` via the `MedicalRecord`, runs `ISafetyCheckService.CheckPrescriptionAsync(patientId, drugName, excludeMedicationId: null)`. If the result is not allowed (any conflict has `Severity == Critical`) → throw `ConflictException` (mapped to **409**) with the conflict list. Otherwise create `Medication` (`Status=Active`) and return `201 Created` with `PrescribeMedicationResponseDto { Medication, Warnings }`. Audit `CREATE_MEDICATION`.
- `PUT {id}/discontinue` — `[Authorize(Roles="Doctor")]`. Sets `Status=Discontinued`. `404` if not found. Idempotent if already discontinued (returns `200 OK` with current `MedicationDto`). Audit `DISCONTINUE_MEDICATION`.

**`IMedicationService`**: `PrescribeAsync(CreateMedicationDto, int currentUserId)`, `DiscontinueAsync(int id)`. The critical-conflict gate is owned by this service (it calls the safety service and throws on a Critical conflict). The `MedicationStatus` transition invariant (Active→Discontinued only; cannot re-activate) is owned here: `DiscontinueAsync` sets Discontinued regardless of current status (idempotent), and there is no endpoint to revert.

DTOs (`DTOs/Medications/`):
- `CreateMedicationDto`: `RecordId` [Required], `DrugName` [Required, MaxLength 100], `Dosage` [Required, MaxLength 50], `Frequency` [Required, MaxLength 50], `StartDate` [optional, defaults to now; if provided **must not be in the future** — service-enforced `BusinessRuleException` → 400; not a data annotation].
- `MedicationDto`: `MedicationId, RecordId, DrugName, Dosage, Frequency, StartDate, Status`. Implements `IAuditableResult.EntityId => MedicationId`.
- `PrescribeMedicationResponseDto`: `Medication: MedicationDto`, `Warnings: IList<SafetyWarningDto>`.
- `SafetyWarningDto`: `RuleId, Type, Severity, Message, RelatedDrug?, RelatedAllergen?` (mapped 1:1 from `SafetyConflict`).

Repository: `IMedicationRepository.GetActiveByPatientAsync(patientId)` (via records), `GetByIdAsync(id)`.

### 5.7 Appointments — `AppointmentsController` (`[Route("api/appointments")]`)
- `POST ""` — `[Authorize(Roles="Doctor,Nurse,Administrator")]`. Request `CreateAppointmentDto`. Validates `PatientId` exists (404), `DoctorId` exists and `Role==Doctor` (else `400 "Selected user is not a Doctor."`), `AppointmentDate` is in the future (else `400`). Creates with `Status=Scheduled`. Returns `201 Created` with `AppointmentDto`. Audit `CREATE_APPOINTMENT`.
- `GET doctor/{doctorId}` — `[Authorize(Roles="Doctor,Nurse,Administrator")]`. Validates the user is a Doctor (else `400`). Returns `200 OK` with `IList<AppointmentDto>` ordered by `AppointmentDate`. Empty list if none.

**`IAppointmentService`**: `CreateAsync(CreateAppointmentDto)`, `GetByDoctorAsync(int doctorId)`. The "DoctorId must be a Doctor" invariant and the "AppointmentDate must be in the future" invariant are owned by the service (both → `BusinessRuleException` → 400; no built-in `[Future]` data annotation).

DTOs (`DTOs/Appointments/`):
- `CreateAppointmentDto`: `PatientId` [Required], `DoctorId` [Required], `AppointmentDate` [Required] (DateTime; must be in the future — service-enforced).
- `AppointmentDto`: `AppointmentId, PatientId, DoctorId, AppointmentDate, Status, PatientName?, DoctorName?`. Implements `IAuditableResult.EntityId => AppointmentId`.

Repository: `IAppointmentRepository.GetByDoctorAsync(doctorId)`.

### 5.8 Reporting & Audit
`ReportsController` (`[Route("api/reports")]`):
- `GET monthly-stats` — `[Authorize(Roles="Administrator")]`. Query `month` (int, default current UTC month), `year` (int, default current UTC year). **Range validation (not pagination):** `month` must be 1–12 and `year` must be a plausible positive year (e.g. ≥ 2000); out of range → `400 BusinessRuleException` (these are explicit filter values, not page clamps). Returns `200 OK` with `MonthlyStatsDto`.

`AuditController` (`[Route("api/audit")]`):
- `GET ""` — `[Authorize(Roles="Administrator")]`. Query `page`, `pageSize` (unified pagination rule from 5.3: clamped `page=max(1,page)`, `pageSize=clamp(pageSize,1,100)`). Returns `200 OK` with `PagedResult<AuditLogDto>` ordered by `Timestamp DESC`.

**`IReportService.GetMonthlyStatsAsync(int month, int year)`** validates month/year ranges (throws `BusinessRuleException` → 400 if invalid) then computes (all filtered by year/month of the relevant date, **UTC** — see known limitation in Section 14):
- `TotalVisits` = count of `MedicalRecords` where `VisitDate` in month.
- `NewPatientRegistrations` = count of `Patients` where `CreatedAt` in month.
- `LabTestsOrdered` = count of `LabResults` where `DateOrdered` in month.
- `DepartmentPerformance` = group `MedicalRecords` by `Doctor.Department` (null → `"Unassigned"`), count.

DTOs (`DTOs/Reports/`, `DTOs/Audit/`):
- `MonthlyStatsDto`: `Month, Year, TotalVisits, NewPatientRegistrations, LabTestsOrdered, DepartmentPerformance: IList<DepartmentStatDto>`.
- `DepartmentStatDto`: `Department, VisitCount`.
- `AuditLogDto`: `AuditId, UserId, Username?, Action, Details, Timestamp`.

Repository: `IAuditLogRepository.GetPagedAsync(page, pageSize)` returns `(items, total)` ordered by Timestamp desc. Reports queries use `IRepository<MedicalRecord>`/`<Patient>`/`<LabResult>` via `Query()`.

---

## 6. CDS Engine Design

The CDS engine is a **real, rule-driven service** with a defined, extensible ruleset — no stubs.

### Name normalization (applies to all matching)
All drug-name and allergen comparisons use **`string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase)`**. Seed rule names are canonical (e.g. `"Warfarin"`, `"Penicillin"`, `"Sulfa"`). The README documents that prescribers must use the canonical drug names for matching to work (a future controlled drug catalog would remove this free-text reliance). Allergy matching is **exact, case-insensitive token membership** of the allergen string against the patient's `PatientAllergy.Allergen` rows — the same matching semantics used for drug names (not substring containment). This resolves the allergen-matching ambiguity.

### Rule models (`Models/Cds/`)
Five rule kinds, all in `IClinicalRuleStore` (single source of truth, extensible):
- `DiagnosticRule { string Id, string Name, IList<string> TriggerSymptoms, int MinSymptomMatches, string SuggestedDiagnosis, CdsSeverity Severity }` — fires when the patient's symptoms contain ≥ `MinSymptomMatches` of `TriggerSymptoms` (case-insensitive).
- `DrugInteractionPair { string Id, string DrugA, string DrugB, CdsSeverity Severity, string Description }` — fires when the medication list contains both drugs (case-insensitive exact match on normalized `DrugName`).
- `AllergyConflict { string Id, string Allergen, IList<string> Drugs, CdsSeverity Severity, string Description }` — fires when a patient's allergy rows contain `Allergen` (exact case-insensitive) and the medication list contains any of `Drugs` (case-insensitive exact).
- `CriticalLabRule { string Id, string TestName, double? CriticalLow, double? CriticalHigh, CdsSeverity Severity, string Description }` — fires when a lab result's numeric value (from `IReferenceRangeParser.ParseNumericValue`) is `< CriticalLow` or `> CriticalHigh` (`TestName` matched case-insensitive `Contains`). **Uses the numeric extractor, which parses threshold forms to their bound** (e.g. a `"<2"` glucose → 2 → below `CriticalLow 2.5` → critical-low). This path is intentionally numeric.
- `DrugLabConflictRule { string Id, string Drug, string LabTest, string Operator ("<" or ">"), double Threshold, CdsSeverity Severity, string Description }` — fires when the patient's latest lab result for `LabTest` satisfies `Operator Threshold` and the medication list contains `Drug` (case-insensitive exact).

Supporting models:
- `CdsSeverity { Info, Warning, High, Critical }`.
- `CdsFindingType { Diagnostic, Interaction, Allergy, CriticalLab, DrugLab }`.
- `CdsFinding { string RuleId, CdsFindingType Type, CdsSeverity Severity, string Title, string Description, string? RelatedItem }`.
- `CdsEvaluationInput { IList<string> Symptoms, IList<LabResultInput> LabResults, IList<string> Medications, IList<string> Allergies }`.
- `LabResultInput { string TestName, string ResultValue, string ReferenceRange }` — the merge/eval unit for lab data.
- `CdsEvaluationResult { IList<CdsFinding> DiagnosticRecommendations, IList<CdsFinding> InteractionWarnings, IList<CdsFinding> CriticalAlerts }`.
- `SafetyConflict { string RuleId, CdsFindingType Type, CdsSeverity Severity, string Message, string? RelatedDrug, string? RelatedAllergen }` — a single conflict surfaced to the medication gate.
- `SafetyCheckResult { bool Allowed, IList<SafetyConflict> Conflicts }` where `Allowed = !Conflicts.Any(c => c.Severity == CdsSeverity.Critical)`.

### Seed ruleset (meaningful, capstone-grade)
Diagnostic:
- `DIA-001`: symptoms `[polyuria, polydipsia, weight loss, fatigue]`, min 2 → "Possible Diabetes Mellitus — recommend fasting glucose and HbA1c." Severity High.
- `DIA-002`: symptoms `[fever, cough, dyspnea]`, min 2 → "Possible respiratory infection — consider chest imaging." Severity Warning.
- `DIA-003`: symptoms `[chest pain, dyspnea, diaphoresis]`, min 2 → "Possible acute coronary syndrome — urgent assessment required." Severity Critical.

Drug-drug interactions:
- `INT-001`: Warfarin + Aspirin → "Increased bleeding risk." Critical.
- `INT-002`: Warfarin + Ibuprofen → "Increased GI bleeding risk." Critical.
- `INT-003`: Enalapril + Spironolactone → "Risk of hyperkalemia." High.
- `INT-004`: Simvastatin + Clarithromycin → "Risk of myopathy/rhabdomyolysis." High.

Allergy conflicts (the previous `ALG-002` "Sulfa"/"Sulfonamide" is split into two distinct rules so `AllergyConflict.Allergen` stays a single string):
- `ALG-001`: Allergen `"Penicillin"` → drugs `[Penicillin, Amoxicillin, Ampicillin, Amoxiclav, Flucloxacillin]` → "Penicillin allergy — contraindicated beta-lactam." Critical.
- `ALG-002a`: Allergen `"Sulfa"` → drugs `[Sulfamethoxazole, Co-trimoxazole, Trimethoprim]` → "Sulfa allergy — contraindicated sulfonamide." Critical.
- `ALG-002b`: Allergen `"Sulfonamide"` → drugs `[Sulfamethoxazole, Co-trimoxazole, Trimethoprim]` → "Sulfonamide allergy — contraindicated sulfonamide." Critical.
- `ALG-003`: Allergen `"Aspirin"` → drugs `[Aspirin]` → "Aspirin/NSAID sensitivity." High.

Critical lab values:
- `LAB-001`: Test "Glucose", CriticalHigh 22, CriticalLow 2.5 → "Critical glucose level." Critical.
- `LAB-002`: Test "Potassium", CriticalHigh 6.5, CriticalLow 2.5 → "Critical potassium level." Critical.
- `LAB-003`: Test "Hemoglobin", CriticalHigh 20, CriticalLow 6 → "Critical hemoglobin level." Critical.

Drug-lab conflicts:
- `DLC-001`: Drug "Metformin", LabTest "Creatinine", `>` 130 → "Metformin contraindicated in renal impairment." Critical.
- `DLC-002`: Drug "Ibuprofen", LabTest "Creatinine", `>` 120 → "NSAID caution in renal impairment." High.
- `DLC-003`: Drug "Warfarin", LabTest "INR", `>` 4 → "High bleeding risk with elevated INR." Critical.

### Extensibility
Rules are plain C# objects in a list returned by `IClinicalRuleStore.Get*()`; adding a rule = adding an entry (or, later, loading from a DB table — the interface hides the source). The store is a singleton with static seed data. No code change is needed to add a *data* rule beyond the seed list; new *rule kinds* require a new model + evaluator branch (documented as the only non-data extension path).

### Lab-result scope and recency (consistent across CDS + safety)
Lab-driven rules (`CriticalLabRule`, `DrugLabConflictRule`) evaluate against the **single most-recent result per `TestName`** for the patient (`max(DateOrdered)`), applied identically in `CdsEngine` and `SafetyCheckService`. This prevents stale historical results (e.g. an old abnormal creatinine) from firing after the patient normalizes. A pure helper `LabResultSelector.LatestPerTest(IEnumerable<LabResult> results) → IList<LabResult>` (in `Models/Cds/`, used by both services) implements the grouping: `results.GroupBy(r => r.TestName, StringComparer.OrdinalIgnoreCase).Select(g => g.OrderByDescending(r => r.DateOrdered).First())`. This helper is unit-testable.

**CdsEngine merge with explicit inputs:** when `PatientId` is provided, the engine loads stored latest-per-test lab results, active medications, and `PatientAllergy` rows, then merges with the explicit inputs in the request:
- **Lab results — dedup by normalized `TestName`, explicit overrides stored:** build a `Dictionary<string, LabResultInput>` keyed by normalized test name; seed with the stored latest-per-test (mapped to `LabResultInput`); then overlay explicit `LabResults` from the request (explicit wins for that test; tests only in storage keep their stored value). Other tests not in storage are added from explicit input.
- **Medications / Symptoms / Allergies — union (no dedup):** explicit entries extend the loaded ones (lets a caller pass hypothetical meds/symptoms for "what-if" evaluation). Medication/allergen entries are normalized (trim) before matching and deduped case-insensitively to avoid double-firing the same pair.

### Evaluation pipeline
Two layers (testable separately):
- `ICdsRuleEvaluator.Evaluate(CdsEvaluationInput) → CdsEvaluationResult` — **pure**, no DI/DB. Iterates the five rule sets against the input using `IClinicalRuleStore` + `IReferenceRangeParser` (numeric extraction). Unit-testable.
- `ICdsEngine.EvaluateAsync(CdsEvaluationRequestDto) → CdsEvaluationResponseDto` — **integration**. If `PatientId` is provided, loads the patient's allergies (`IPatientAllergyRepository`), active medications (via records), and lab results (via records) from repos, computes latest-per-test, and **merges** them with the explicit inputs per the rules above. Builds `CdsEvaluationInput`, calls the evaluator, maps to the response DTO grouped into `DiagnosticRecommendations`, `InteractionWarnings`, `CriticalAlerts` (Diagnostic rules → recommendations; Interaction + DrugLab → warnings; Allergy + CriticalLab → critical alerts), each a `List<CdsFindingDto>` with `EvaluatedAt`.

DTOs (`DTOs/Cds/`):
- `CdsEvaluationRequestDto { int? PatientId, IList<string>? Symptoms, IList<LabResultInputDto>? LabResults, IList<MedicationInputDto>? Medications, IList<string>? Allergies }`.
- `LabResultInputDto { string TestName [Required], string ResultValue [Required], string ReferenceRange [Required] }`.
- `MedicationInputDto { string DrugName [Required], string? Dosage }`.
- `CdsFindingDto { string RuleId, string Type, string Severity, string Title, string Description, string? RelatedItem }`.
- `CdsEvaluationResponseDto { IList<CdsFindingDto> DiagnosticRecommendations, IList<CdsFindingDto> InteractionWarnings, IList<CdsFindingDto> CriticalAlerts, DateTime EvaluatedAt }`.

### Shared safety-check service
`ISafetyCheckService.CheckPrescriptionAsync(int patientId, string drugName, int? excludeMedicationId) → SafetyCheckResult { bool Allowed, IList<SafetyConflict> Conflicts }` is **reused by the Medication module** (and usable by CDS). It loads the patient's active medications (excluding `excludeMedicationId`), allergies (`PatientAllergy` rows), and latest-per-test lab results, then runs: drug-drug interactions (new `drugName` vs each active med), drug-allergy conflicts (new drug vs patient allergies), and drug-lab conflicts (new drug vs lab values). Returns conflicts with `CdsSeverity`. `MedicationService.PrescribeAsync` throws `ConflictException` when `!result.Allowed` (a Critical conflict); non-critical conflicts become `Warnings` (mapped `SafetyConflict → SafetyWarningDto`) in the response. The same `IClinicalRuleStore` powers both the CDS evaluator and this service (no duplicated rule data). The `SafetyConflict → SafetyWarningDto` mapping is field-for-field: `RuleId→RuleId`, `Type→Type` (as string), `Severity→Severity` (as string), `Message→Message`, `RelatedDrug→RelatedDrug`, `RelatedAllergen→RelatedAllergen`. `Allowed` means "no Critical conflict present" — `MedicationService` is the actual gate that rejects on Critical.

---

## 7. Reference-Range Parsing Strategy

`IReferenceRangeParser` (`Services/ReferenceRangeParser.cs`) is a singleton service used by `LabService` (result upload classification) and the CDS evaluator (numeric lab values). It is **pure** and unit-testable. It exposes two operations:

- `ParseRange(string referenceRange) → ReferenceRangeParseResult { double? Min, double? Max, string? Unit }` — bounds for classification.
- `ParseNumericValue(string resultValue) → double?` — extracts a number for rule comparison.
- `Classify(string resultValue, string referenceRange) → LabResultStatus` — the Normal/High/Low/Indeterminate classification used by `LabService`.

`ReferenceRangeParseResult` lives in `Models/ReferenceRangeParseResult.cs` (note: it carries `Min?`, `Max?`, `Unit?`; `Value`/`Status` are not on this model — classification returns a `LabResultStatus` directly).

### Range parsing (`ParseRange`)
- `"70-110 mg/dL"` → `Min=70, Max=110, Unit="mg/dL"` (range form `<min>-<max> [unit]`).
- `"<5 mg/dL"` → `Max=5, Min=null, Unit="mg/dL"` (normal is below the limit).
- `">40 mg/dL"` → `Min=40, Max=null, Unit="mg/dL"` (normal is above the limit; symmetric support).
- `"100 mg/dL"` (single value, no range) → `Min=null, Max=null` → no bounds.
- Unparseable → `Min=null, Max=null`.

### Numeric extraction (`ParseNumericValue`) — for rule comparison
- Plain number `"70"` → `70`.
- Threshold form `"<5"` → `5`; `">200"` → `200` (the bound is the numeric value for critical-threshold comparison — intentional, see note below).
- Qualitative (`"negative"`, `"positive"`, `"trace"`, `"N/A"`, `""`) → `null`.

### Classification (`Classify`) — for `LabResult.Status`
**Threshold-form result values are classified `Indeterminate`** (the exact numeric value is not known, so the bound must not be compared as if it were the value):
1. If `resultValue` matches a threshold form (`^[<>]\s*\d`), return `Indeterminate` — do not parse the bound as the value.
2. Else parse numeric via `ParseNumericValue`; if `null`, return `Indeterminate`.
3. Else parse the range via `ParseRange`:
   - `Min` and `Max` both present: `value < Min → Low`; `value > Max → High`; else `Normal`.
   - `Max` only (`<X` range): `value >= Max → High`; else `Normal`.
   - `Min` only (`>X` range): `value <= Min → Low`; else `Normal`.
   - No bounds (single-value range) → `Indeterminate`.

**Why the split:** a result reported `"<5"` against a `"<5 mg/dL"` normal range is clearly below the limit (normal), but treating `5` as the value would make `value >= Max(5)` → `High` (wrong, alert fatigue). So classification returns `Indeterminate` for threshold forms, while `CriticalLabRule`/`DrugLabConflictRule` keep using `ParseNumericValue` (threshold forms parse to their bound) — a `"<2"` glucose is genuinely below `CriticalLow 2.5` and must still fire the critical-low alert. The two paths serve different questions ("what is the value precisely?" vs "does it cross a critical threshold?") and are both correct.

### Edge-case handling
- **Unparseable** (value or range) → `Indeterminate`; `LabService` still stores the `ResultValue` and returns `200` with `Status=Indeterminate` (graceful — never rejects the upload).
- **Units mismatch** between result and range: no auto-conversion (out of scope); classification proceeds numerically and a warning is logged via `ILogger`. Documented simplification.
- **Null `ResultValue` at order time**: ordering sets `Status=Ordered`; classification only runs on `PUT results/{id}`.

Invariant owner: `LabService` owns the classification (it is the only place `Status` transitions out of `Ordered`). The parser is stateless and side-effect free.

---

## 8. Admin Bootstrap

Because `/api/auth/register` is Administrator-only, a seed administrator is required to bootstrap. **Decision: seed a default admin on startup if no Administrator user exists**, configured via `appsettings.json` → `AdminSeed`. Implemented by `IAdminSeeder.SeedAsync()`.

`AdminSeedSettings { string Username, string Password, string FullName, string Email, string? Department }`. `AdminSeeder.SeedAsync()`: resolve `IUserRepository` + `PasswordHasher<User>` from a startup scope; if no user with `Role==Administrator` exists:
- If `AdminSeedSettings.Password` is a non-empty, non-placeholder value → hash it and create the admin.
- If `Password` is null/empty/whitespace → **generate a cryptographically random password** (`RandomNumberGenerator`, ~16 chars, alphanumeric), hash it, create the admin, and log it **once** at `Warning`: `"Seeded initial administrator '{Username}' with generated password '{password}'. Rotate it immediately."` (one-time bootstrap; the operator captures it from the console log).

Idempotent — runs every startup, no-op if an admin exists. The seeded admin has `Department = null`. **No usable default password is committed to the repo** (base `appsettings.json` ships `AdminSeed:Password` as `""`, i.e. empty → random generation; `appsettings.Development.json` overrides it with a known dev password so local dev is runnable). Program.cs wiring: after `app.Build()`, create a scope, resolve `IAdminSeeder`, `await seeder.SeedAsync()`.

**First-login password change**: there is **no forced-change flow** in this capstone scope (no `MustChangePassword` flag or change-password endpoint in the spec). Instead the expectation is **documented** in the README: the seeded/generated password is a bootstrap credential and must be rotated — the recommended rotation path is for an administrator to register a new admin and disable the seed account, or override `AdminSeed__Password` and re-seed; a future password-change endpoint is a backlog item. This satisfies "password must be changed on first login **OR documented**" — the documented route is chosen to avoid adding out-of-scope auth features.

---

## 9. Audit Logging

**Decision: an MVC action filter (`AuditActionFilter`) plus a declarative `AuditActionAttribute`**, consistent across all sensitive endpoints (mutations **and** PHI reads). Rejected alternatives: middleware (lacks business semantics — cannot map a request to a meaningful `Action`/entity id); explicit service calls from each controller (scatters audit logic, easy to forget, not "automatic").

`AuditActionAttribute(string actionName, string? idArgument = null)` is placed on every audited action. `idArgument` names the route parameter that holds the entity id for by-id actions (e.g. `"id"`, `"patientId"`); for POST creates it is omitted (the id comes from the result). The global `AuditActionFilter` (`IAsyncActionFilter`, registered via `MvcOptions.Filters.Add<AuditActionFilter>()`) runs on `OnActionExecutionAsync`:

1. Read the attribute via the filter's `ActionDescriptor` (the `AuditActionAttribute` on the action method). If absent, do nothing and call `next()`.
2. Resolve the actor: `ICurrentUserService.GetUserId()` (JWT `sub` claim) → nullable `UserId`.
3. **Execute the action inside `try { executedContext = await next(); }` so audit always runs in `finally`.**
4. Determine **success**: `success = executedContext.Exception == null || executedContext.ExceptionHandled`. (A thrown, unhandled exception — e.g. a failed login that threw `UnauthorizedAccessException` — is `success=false`; the exception still propagates to `GlobalExceptionMiddleware` because the filter does **not** set `ExceptionHandled`.)
5. **Action-name switching for login:** if the declared action is `"LOGIN"` and `!success`, emit `"LOGIN_FAILED"`; otherwise emit the declared action name.
6. **Compose `Details` as a strict allowlist** (never the request DTO body, never PII):
   - For `LOGIN`/`LOGIN_FAILED`: `Details = { "username": <LoginDto.Username from action args>, "success": <bool> }`.
   - For by-id actions (attribute has `idArgument` and the arg exists): `Details = { "entityId": <id value>, "success": <bool> }`.
   - For POST creates (no `idArgument`): read the created entity id from the result — if `executedContext.Result` is an `ObjectResult` whose value implements `IAuditableResult`, `Details = { "entityId": <IAuditableResult.EntityId>, "success": <bool> }`; else `Details = { "entityId": null, "success": <bool> }`.
   - For `CDS_EVALUATE`: `Details = { "success": <bool> }` (optionally `patientId` from the request DTO if provided — an identifier, not PII).
   - Serialize the dictionary to a JSON string via `System.Text.Json.JsonSerializer.Serialize` and store in `AuditLog.Details`.
7. `await IAuditLogService.LogAsync(userId, effectiveActionName, detailsJson)` with `Timestamp = DateTime.UtcNow` **inside the `finally`**. Audit failures must not fail the request → the service swallows persistence errors and logs them via `ILogger`.

`IAuditableResult { int? EntityId { get; } }` (in `DTOs/Common/IAuditableResult.cs`) is implemented by the create-response DTOs that represent a created entity: `UserDto` (`UserId`), `PatientDetailDto` (`PatientId`), `MedicalRecordDto` (`RecordId`), `LabResultDto` (`ResultId`), `MedicationDto` (`MedicationId`), `AppointmentDto` (`AppointmentId`). This gives the filter a typed, reflection-free way to read the created id from `context.Result`.

`IAuditLogService.LogAsync(int? userId, string action, string details)` persists an `AuditLog` row. **Reliability:** the service uses `IDbContextFactory<DcdssContext>` to create an isolated, short-lived DbContext (separate from the request's scoped DbContext) so an audit entry is recorded even when the request's own DbContext is in a failed/rolled-back state (e.g. a 409 conflict, a failed login). This is the concrete answer to "what happens if the action threw" — audit still writes.

What is logged (action names): `LOGIN`, `LOGIN_FAILED`, `REGISTER`, `CREATE_PATIENT`, `UPDATE_PATIENT`, `VIEW_PATIENT`, `CREATE_MEDICAL_RECORD`, `CDS_EVALUATE`, `CREATE_LAB_ORDER`, `UPDATE_LAB_RESULT`, `VIEW_LAB_HISTORY`, `CREATE_MEDICATION`, `DISCONTINUE_MEDICATION`, `CREATE_APPOINTMENT`. Fields stored: `UserId?`, `Action`, `Details` (allowlist JSON), `Timestamp`.

**POPIA:** `Details` is an **allowlist of identifiers and a success flag only** — it never contains passwords, hashes, tokens, secrets, names, addresses, DOB, phone, or allergy/clinical text. This is enforced structurally (the filter composes `Details` only from route ids, the `IAuditableResult.EntityId`, the login username, and the success flag — never from the request DTO). This corrects revision 1's denylist, which would have serialized `CreatePatientDto` (name/address/phone/DOB/allergies) into `Details`.

`GET /api/audit` (Administrator) returns paged entries (Section 5.8).

---

## 10. JWT Design

- `ITokenService.GenerateToken(User user) → string` (impl `JwtTokenService`, depends on `IOptions<JwtSettings>`). Claims (short names): `sub` = `UserId.ToString()`, `role` = `UserRole.ToString()`, `name` = `Username`. Signing: HMAC-SHA256 with the symmetric `SigningKey` from `JwtSettings`; `Issuer`/`Audience` from settings; `Expires` = `DateTime.UtcNow.AddMinutes(ExpiryMinutes)`.
- Program.cs: `AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(...)` with `TokenValidationParameters { ValidateIssuer=true, ValidateAudience=true, ValidateLifetime=true, ValidateIssuerSigningKey=true, ValidIssuer, ValidAudience, IssuerSigningKey=new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)), ClockSkew=TimeSpan.FromMinutes(1), NameClaimType = "name", RoleClaimType = "role" }`; then `AddAuthorization(...)` with the fallback policy (Section 5). The bearer options read `JwtSettings` via the options pattern.
- **Explicit claim-type mapping (robustness):** `RoleClaimType = "role"` and `NameClaimType = "name"` are set explicitly so RBAC does not depend on the default JWT inbound claim map; the token emits short-named `role`/`name` claims that match. A future change to the default map cannot silently break `[Authorize(Roles=...)]`.
- `LoginResponseDto` includes `Token`, `Username`, `Role`, `ExpiresAt`.
- **Signing-key fail-fast:** a `JwtSettingsValidator : IValidateOptions<JwtSettings>` (registered `services.AddSingleton<IValidateOptions<JwtSettings>, JwtSettingsValidator>()` with `services.AddOptions<JwtSettings>().Bind(...).ValidateOnStart()`) throws on startup if `SigningKey` is null/empty/whitespace, equals the placeholder `"REPLACE_ME"`, or is `< 32` UTF-8 bytes. The base `appsettings.json` ships `SigningKey: "REPLACE_ME"` (rejected by the validator → app fails to start if an operator does not override it); `appsettings.Development.json` overrides it with a usable ≥32-byte dev key. The key is **never logged or returned** (the `UserDto`/`LoginResponseDto` shapes and the audit `Details` allowlist guarantee this).

---

## 11. Configuration Design

Typed `IOptions<T>` classes in `Configuration/`:
- `JwtSettings { string Issuer, string Audience, string SigningKey, int ExpiryMinutes }`.
- `CorsSettings { string AllowedOrigins }` (comma-separated; split in DI).
- `AdminSeedSettings { string Username, string Password, string FullName, string Email, string? Department }`.

`appsettings.json` schema (exact keys — note `SigningKey` is a rejected placeholder and `AdminSeed.Password` is empty, so no usable secret is committed):
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Port=3306;Database=dcdss;User=root;Password=changeme;AllowPublicKeyRetrieval=True;"
  },
  "JwtSettings": {
    "Issuer": "DCDSS",
    "Audience": "DCDSS",
    "SigningKey": "REPLACE_ME",
    "ExpiryMinutes": 60
  },
  "CorsSettings": {
    "AllowedOrigins": "https://localhost:3000"
  },
  "AdminSeed": {
    "Username": "admin",
    "Password": "",
    "FullName": "System Administrator",
    "Email": "admin@dcdss.local",
    "Department": null
  },
  "Logging": { "LogLevel": { "Default": "Information", "Microsoft.AspNetCore": "Warning" } },
  "AllowedHosts": "*"
}
```

`appsettings.Development.json` overrides (relaxed dev values — note the real dev signing key and a known dev admin password so local dev runs without env-var setup):
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Port=3306;Database=dcdss_dev;User=root;Password=changeme;AllowPublicKeyRetrieval=True;"
  },
  "CorsSettings": { "AllowedOrigins": "https://localhost:3000,http://localhost:5173" },
  "JwtSettings": {
    "SigningKey": "DevOnlySigningKey_32bytes_minimum_DO_NOT_USE_IN_PROD",
    "ExpiryMinutes": 480
  },
  "AdminSeed": { "Password": "Admin@12345" }
}
```

Binding: `builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("JwtSettings"))` (with `.ValidateOnStart()` and the validator from Section 10), likewise `Configure<CorsSettings>(...)` and `Configure<AdminSeedSettings>(...)`.

**Production hardening (documented in README):** override `JwtSettings__SigningKey` (≥32 random bytes) and `AdminSeed__Password` (or let the seeder generate one) via environment variables / user-secrets. The app will not start in production without a valid signing key (fail-fast).

---

## 12. Global Exception Handling Middleware

`GlobalExceptionMiddleware` (`Middleware/GlobalExceptionMiddleware.cs`, registered `app.UseMiddleware<GlobalExceptionMiddleware>()` **first** in the pipeline) catches every unhandled exception, logs via `ILogger<GlobalExceptionMiddleware>` with a generated `traceId`, and writes an RFC 7807 `ProblemDetails` response with `Content-Type: application/problem+json`. Stack traces and raw exception messages are **never** sent to clients (Development may include `Detail` for diagnostics via `builder.Environment.IsDevelopment()`).

Mapping:
| Exception | Status | Title | Notes |
|---|---|---|---|
| `NotFoundException` | 404 | "Not Found" | entity missing; logged at Warning |
| `ConflictException` | 409 | "Conflict" | duplicate username/email, critical drug conflict; logs the conflict list; logged at Warning |
| `BusinessRuleException` | 400 | "Business Rule Violation" | e.g. DOB in future, doctor-role mismatch, future-appointment-date, out-of-range report month/year; logged at Warning |
| `UnauthorizedAccessException` | 401 | "Unauthorized" | login failure; logged at Information |
| `DbUpdateException` | 409 | "Data Conflict" | DB constraint violation; generic detail; logged at Error |
| Any other | 500 | "Internal Server Error" | logged at Error with stack trace (logs only) |

`ValidationProblemDetails` for invalid DTOs is produced automatically by `[ApiController]` (ModelState → 400 `application/problem+json`) — the middleware does not need to handle model-state failures. Each response includes `Status`, `Title`, `Detail`, `Instance` (request path), and `traceId` extension. Per-exception logging level is as above; 500s log the full stack trace to the logger but never to the response.

---

## 13. Migration Approach

`dotnet ef` is **not installed** in this environment. The implementer must create the `InitialCreate` migration. Two paths, in order of preference:

1. **Install the tool, then scaffold (preferred)**:
   ```
   dotnet tool install --global dotnet-ef --version 8.0.33
   cd DCDSS
   dotnet ef migrations add InitialCreate --project DCDSS --startup-project DCDSS
   dotnet ef database update --project DCDSS --startup-project DCDSS
   ```
2. **Manual fallback (if the tool cannot be installed)**: author `Migrations/<timestamp>_InitialCreate.cs`, `Migrations/<timestamp>_InitialCreate.Designer.cs`, and `Migrations/DcdssContextModelSnapshot.cs` by hand, matching `DcdssContext` + the `IEntityTypeConfiguration<T>` files, then run `dotnet ef database update` (if the tool is later available) or apply via a generated SQL script. The README documents both.

**Design-time MySQL version**: `AddDbContextFactory<DcdssContext>` uses `ServerVersion` explicitly — `new MySqlServerVersion(new Version(8, 0, 34))` — **not** `ServerVersion.AutoDetect(connectionString)`, so migrations scaffold **without a live database connection**. (Only `AddDbContextFactory` is registered; it also registers a scoped `DcdssContext` resolved from the factory, which the repositories consume — there is no separate `AddDbContext` registration.)

`dotnet build` must pass without a database. MySQL connectivity is only needed at runtime/`database update` time.

---

## 14. README Outline

Written to `$ROOT/README.md`. Sections:
1. **Title & Overview** — what DCDSS is, the rural-SA context, role list.
2. **Prerequisites** — .NET 8 SDK (8.0.427+), MySQL 8, `dotnet-ef` tool (install command).
3. **Restore & Build** — `dotnet restore`, `dotnet build` (from `$ROOT` or `$PROJ`).
4. **Configure MySQL** — edit `DCDSS/appsettings.json` `ConnectionStrings:DefaultConnection`; note `AllowPublicKeyRetrieval` for MySQL 8 auth; create the database.
5. **EF Core Migrations** — install `dotnet-ef`, `migrations add InitialCreate`, `database update` (commands from Section 13); note the explicit server version so no live DB is needed to scaffold.
6. **Initial Administrator** — seeded on first startup from `AdminSeed`; in Development the seeded password is `Admin@12345` (from `appsettings.Development.json`); in Production `AdminSeed:Password` is empty so the seeder generates a random password and logs it once at Warning — capture it from the console and rotate it; **must change the password** (future change-password endpoint is backlog); bootstrap flow: seed admin → login → register staff.
7. **Run the API** — `dotnet run --project DCDSS`; Swagger at `/swagger`; health at `/health`.
8. **Authentication** — login to get JWT, click Authorize in Swagger, paste `Bearer <token>`. Note the JWT signing key must be overridden in production (`JwtSettings__SigningKey`, ≥32 bytes); the app fails to start with the placeholder key.
9. **Endpoint Reference** — table of all endpoints (method, path, role, request body, sample `curl`), one sample request per endpoint (incl. `POST /api/medical-records` to create a visit before ordering labs / prescribing).
10. **Project Structure** — the folder/namespace map (Section 1) and layering rules.
11. **Assumptions & Known Limitations** — the documented deviations: `PatientAllergy` normalized table; `AuditLog.UserId` nullable; `LabResultStatus.Indeterminate` (incl. threshold-form values); `MedicalRecordStatus` set; no DELETE endpoints; all-FK Restrict; units not converted; **UTC storage** (report month/year boundaries and appointment display times are UTC, so a SAST day/month can appear shifted by up to 2 hours — time-zone-aware storage is a backlog item); drug/allergen names must match canonical seed names (free-text today; controlled drug catalog is a future item).

---

## 15. File-by-File Implementation Outline

All paths absolute. `$ROOT` = `c:/Users/nm/IdeaProjects/Digital Clinical Decision Support System (DCDSS)`, `$PROJ` = `$ROOT/DCDSS`.

### Existing files to modify
- `$PROJ/Program.cs` — full DI + middleware pipeline wiring (Section 16 below).
- `$PROJ/appsettings.json` — schema in Section 11 (no usable committed secrets).
- `$PROJ/appsettings.Development.json` — dev overrides (Section 11).
- `$ROOT/README.md` — Section 14.

### API layer — Controllers
- `$PROJ/Controllers/AuthController.cs` — `POST register` (Admin), `POST login` (anon); depends on `IAuthService`.
- `$PROJ/Controllers/MedicalRecordsController.cs` — `POST ""` create visit (Doctor/Nurse); depends on `IMedicalRecordService`. **(NEW)**
- `$PROJ/Controllers/PatientsController.cs` — list/create/get/update; depends on `IPatientService`.
- `$PROJ/Controllers/CdsController.cs` — `POST evaluate`; depends on `ICdsEngine`.
- `$PROJ/Controllers/LaboratoryController.cs` — order/update-results/patient-history; depends on `ILabService`.
- `$PROJ/Controllers/MedicationsController.cs` — prescribe/discontinue; depends on `IMedicationService`.
- `$PROJ/Controllers/AppointmentsController.cs` — create/by-doctor; depends on `IAppointmentService`.
- `$PROJ/Controllers/ReportsController.cs` — `GET monthly-stats`; depends on `IReportService`.
- `$PROJ/Controllers/AuditController.cs` — `GET` paged audit; depends on `IAuditLogRepository`.

### API layer — Middleware
- `$PROJ/Middleware/GlobalExceptionMiddleware.cs` — catch-all → RFC 7807 ProblemDetails + ILogger (Section 12).
- `$PROJ/Middleware/AuditActionFilter.cs` — global `IAsyncActionFilter` (Section 9): try/finally, success detection, LOGIN→LOGIN_FAILED switching, allowlist `Details`.
- `$PROJ/Middleware/AuditActionAttribute.cs` — `[AuditAction("ACTION", idArgument?: "id")]` marker.

### Core layer — Services / Interfaces (abstractions only)
- `$PROJ/Services/Interfaces/IRepository.cs` — generic `IRepository<T>`.
- `$PROJ/Services/Interfaces/IUnitOfWork.cs` — `SaveChangesAsync()`.
- `$PROJ/Services/Interfaces/IAuthService.cs` — register/login.
- `$PROJ/Services/Interfaces/ITokenService.cs` — `GenerateToken(User)`.
- `$PROJ/Services/Interfaces/IPatientService.cs`.
- `$PROJ/Services/Interfaces/IMedicalRecordService.cs` — `CreateAsync`. **(NEW)**
- `$PROJ/Services/Interfaces/ICdsEngine.cs` — `EvaluateAsync(CdsEvaluationRequestDto)`.
- `$PROJ/Services/Interfaces/ICdsRuleEvaluator.cs` — pure `Evaluate(CdsEvaluationInput)`.
- `$PROJ/Services/Interfaces/ILabService.cs`.
- `$PROJ/Services/Interfaces/IMedicationService.cs`.
- `$PROJ/Services/Interfaces/IAppointmentService.cs`.
- `$PROJ/Services/Interfaces/IReportService.cs`.
- `$PROJ/Services/Interfaces/IAuditLogService.cs` — `LogAsync(int?, string, string)` (IDbContextFactory).
- `$PROJ/Services/Interfaces/ISafetyCheckService.cs` — `CheckPrescriptionAsync`.
- `$PROJ/Services/Interfaces/IClinicalRuleStore.cs` — rule-set accessor (singleton).
- `$PROJ/Services/Interfaces/IReferenceRangeParser.cs` — parse range + classify + numeric extract.
- `$PROJ/Services/Interfaces/IAdminSeeder.cs` — `SeedAsync()`.
- `$PROJ/Services/Interfaces/ICurrentUserService.cs` — read actor from claims (also referenced from Security/).

### Core layer — Services (implementations)
- `$PROJ/Services/AuthService.cs` — `IAuthService` impl (PasswordHasher + uniqueness).
- `$PROJ/Services/TokenService.cs` — `ITokenService` impl (JwtSettings).
- `$PROJ/Services/PatientService.cs`.
- `$PROJ/Services/MedicalRecordService.cs` — create visit + role/future validation. **(NEW)**
- `$PROJ/Services/CdsEngine.cs` — integration orchestration (DB load + merge, latest-per-test).
- `$PROJ/Services/CdsRuleEvaluator.cs` — pure rule evaluation.
- `$PROJ/Services/LabService.cs` — order/update-result/classify.
- `$PROJ/Services/MedicationService.cs` — prescribe (safety gate)/discontinue.
- `$PROJ/Services/AppointmentService.cs`.
- `$PROJ/Services/ReportService.cs` — monthly stats queries.
- `$PROJ/Services/AuditLogService.cs` — isolated DbContextFactory write.
- `$PROJ/Services/SafetyCheckService.cs` — interaction/allergy/drug-lab checks (latest-per-test).
- `$PROJ/Services/ClinicalRuleStore.cs` — seeded rule lists (Section 6).
- `$PROJ/Services/ReferenceRangeParser.cs` — parse range + classify + numeric extract (Section 7).
- `$PROJ/Services/AdminSeeder.cs` — seed default admin (random password if empty).
- `$PROJ/Configuration/JwtSettingsValidator.cs` — `IValidateOptions<JwtSettings>` fail-fast. **(NEW)**

### Core layer — Models (non-entity domain)
- `$PROJ/Models/Cds/DiagnosticRule.cs`
- `$PROJ/Models/Cds/DrugInteractionPair.cs`
- `$PROJ/Models/Cds/AllergyConflict.cs`
- `$PROJ/Models/Cds/CriticalLabRule.cs`
- `$PROJ/Models/Cds/DrugLabConflictRule.cs`
- `$PROJ/Models/Cds/CdsFinding.cs`
- `$PROJ/Models/Cds/CdsSeverity.cs`
- `$PROJ/Models/Cds/CdsFindingType.cs`
- `$PROJ/Models/Cds/CdsEvaluationInput.cs`
- `$PROJ/Models/Cds/CdsEvaluationResult.cs`
- `$PROJ/Models/Cds/LabResultInput.cs` — `{ string TestName, string ResultValue, string ReferenceRange }`. **(NEW, was referenced but undefined)**
- `$PROJ/Models/Cds/SafetyConflict.cs` — `{ string RuleId, CdsFindingType Type, CdsSeverity Severity, string Message, string? RelatedDrug, string? RelatedAllergen }`. **(fields defined)**
- `$PROJ/Models/Cds/SafetyCheckResult.cs` — `{ bool Allowed, IList<SafetyConflict> Conflicts }`. **(NEW, was referenced but undefined)**
- `$PROJ/Models/Cds/LabResultSelector.cs` — pure `LatestPerTest(IEnumerable<LabResult>)` helper. **(NEW)**
- `$PROJ/Models/ReferenceRangeParseResult.cs` — `{ double? Min, double? Max, string? Unit }`.

### Core layer — DTOs (request/response + validation)
- `$PROJ/DTOs/Common/PagedResult.cs` — generic `{ Items, Total, Page, PageSize }`.
- `$PROJ/DTOs/Common/IAuditableResult.cs` — `{ int? EntityId { get; } }` marker for audit. **(NEW)**
- `$PROJ/DTOs/Auth/RegisterDto.cs`, `LoginDto.cs`, `UserDto.cs` (impl `IAuditableResult`), `LoginResponseDto.cs`.
- `$PROJ/DTOs/MedicalRecords/CreateMedicalRecordDto.cs`, `MedicalRecordDto.cs` (impl `IAuditableResult`; references `LabResultDto`, `MedicationDto`). **(NEW module)**
- `$PROJ/DTOs/Patients/CreatePatientDto.cs` (`Allergies: IList<string>?`), `UpdatePatientDto.cs` (same), `PatientSummaryDto.cs`, `PatientDetailDto.cs` (impl `IAuditableResult`; `Allergies: IList<string>` + `MedicalHistory`).
- `$PROJ/DTOs/Cds/CdsEvaluationRequestDto.cs`, `LabResultInputDto.cs`, `MedicationInputDto.cs`, `CdsFindingDto.cs`, `CdsEvaluationResponseDto.cs`.
- `$PROJ/DTOs/Laboratory/OrderLabTestDto.cs`, `UpdateLabResultDto.cs`, `LabResultDto.cs` (impl `IAuditableResult`).
- `$PROJ/DTOs/Medications/CreateMedicationDto.cs`, `MedicationDto.cs` (impl `IAuditableResult`), `PrescribeMedicationResponseDto.cs`, `SafetyWarningDto.cs`.
- `$PROJ/DTOs/Appointments/CreateAppointmentDto.cs`, `AppointmentDto.cs` (impl `IAuditableResult`).
- `$PROJ/DTOs/Reports/MonthlyStatsDto.cs`, `DepartmentStatDto.cs`.
- `$PROJ/DTOs/Audit/AuditLogDto.cs`.

### Data layer — Entities + Enums
- `$PROJ/Entities/Enums/UserRole.cs`, `Gender.cs`, `BloodType.cs`, `MedicalRecordStatus.cs`, `LabResultStatus.cs`, `MedicationStatus.cs`, `AppointmentStatus.cs`.
- `$PROJ/Entities/User.cs`, `Patient.cs`, `PatientAllergy.cs` **(NEW)**, `MedicalRecord.cs`, `LabResult.cs`, `Medication.cs`, `Appointment.cs`, `AuditLog.cs`.

### Data layer — DbContext + Configurations
- `$PROJ/Data/DcdssContext.cs` — `DbSet<T>` per entity (8); `OnModelCreating` calls `ApplyConfigurationsFromAssembly`; uses explicit `MySqlServerVersion("8.0.34")` via `AddDbContextFactory`.
- `$PROJ/Data/Configurations/UserConfiguration.cs`, `PatientConfiguration.cs`, `PatientAllergyConfiguration.cs` **(NEW)**, `MedicalRecordConfiguration.cs`, `LabResultConfiguration.cs`, `MedicationConfiguration.cs`, `AppointmentConfiguration.cs`, `AuditLogConfiguration.cs` — each: PK, `HasColumnName` to schema names, `HasConversion<string>()` on enums, `HasMaxLength`, required, FKs + Restrict, unique indexes, filter indexes (Section 4). `UserConfiguration` sets `PasswordHash` `MaxLength 512`.

### Data layer — Repositories / Interfaces
- `$PROJ/Repositories/Interfaces/IUserRepository.cs` — `GetByUsernameAsync`, `GetByEmailAsync`.
- `$PROJ/Repositories/Interfaces/IPatientRepository.cs` — `SearchAsync`, `GetByIdWithHistoryAsync`.
- `$PROJ/Repositories/Interfaces/IPatientAllergyRepository.cs` — `GetByPatientAsync`, `DeleteByPatientAsync`, `AddAsync`. **(NEW)**
- `$PROJ/Repositories/Interfaces/IMedicalRecordRepository.cs` — `GetByIdAsync`. **(methods specified)**
- `$PROJ/Repositories/Interfaces/ILabResultRepository.cs` — `GetByPatientAsync`.
- `$PROJ/Repositories/Interfaces/IMedicationRepository.cs` — `GetActiveByPatientAsync`.
- `$PROJ/Repositories/Interfaces/IAppointmentRepository.cs` — `GetByDoctorAsync`.
- `$PROJ/Repositories/Interfaces/IAuditLogRepository.cs` — `GetPagedAsync`.

### Data layer — Repositories (implementations)
- `$PROJ/Repositories/Repository.cs` — generic `Repository<T>` base over `DcdssContext`. **(MOVED from Services/)**
- `$PROJ/Repositories/UnitOfWork.cs` — `SaveChangesAsync` over `DcdssContext`. **(MOVED from Services/)**
- `$PROJ/Repositories/UserRepository.cs`, `PatientRepository.cs`, `PatientAllergyRepository.cs` **(NEW)**, `MedicalRecordRepository.cs`, `LabResultRepository.cs`, `MedicationRepository.cs`, `AppointmentRepository.cs`, `AuditLogRepository.cs`.

### Security + Configuration
- `$PROJ/Security/CurrentUserService.cs` — `ICurrentUserService` (reads `sub`/`role`/`name` claims from `IHttpContextAccessor`).
- `$PROJ/Configuration/JwtSettings.cs`, `CorsSettings.cs`, `AdminSeedSettings.cs`, `JwtSettingsValidator.cs`.

### Exceptions
- `$PROJ/Exceptions/NotFoundException.cs`, `ConflictException.cs`, `BusinessRuleException.cs`.

### Migrations (created by implementer)
- `$PROJ/Migrations/<timestamp>_InitialCreate.cs`, `..._InitialCreate.Designer.cs`, `DcdssContextModelSnapshot.cs`.

---

## 16. Program.cs Wiring (concrete order)

```csharp
var builder = WebApplication.CreateBuilder(args);

// MVC + audit filter
builder.Services.AddControllers(o => o.Filters.Add<AuditActionFilter>());
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(/* AddSecurityDefinition Bearer + AddSecurityRequirement */);

// Data: DbContextFactory only (also registers a scoped DcdssContext from the factory)
var conn = builder.Configuration.GetConnectionString("DefaultConnection")!;
var serverVersion = new MySqlServerVersion(new Version(8, 0, 34));
builder.Services.AddDbContextFactory<DcdssContext>(o => o.UseMySql(conn, serverVersion));

// Options + JWT fail-fast validator
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("JwtSettings"));
builder.Services.Configure<CorsSettings>(builder.Configuration.GetSection("CorsSettings"));
builder.Services.Configure<AdminSeedSettings>(builder.Configuration.GetSection("AdminSeed"));
builder.Services.AddSingleton<IValidateOptions<JwtSettings>, JwtSettingsValidator>();
builder.Services.AddOptions<JwtSettings>().ValidateOnStart();

// Auth: explicit NameClaimType/RoleClaimType, ClockSkew=1m
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o =>
{
    o.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true, ValidateAudience = true, ValidateLifetime = true, ValidateIssuerSigningKey = true,
        ValidIssuer = jwt.Issuer, ValidAudience = jwt.Audience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
        ClockSkew = TimeSpan.FromMinutes(1),
        NameClaimType = "name", RoleClaimType = "role"
    };
});

// Authorization: global authenticated-by-default fallback + per-action role gates
builder.Services.AddAuthorization(o => o.FallbackPolicy =
    new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

// CORS
builder.Services.AddCors(o => o.AddPolicy("Default", b => b
    .WithOrigins(cors.AllowedOrigins.Split(',')).AllowAnyHeader().AllowAnyMethod()));

// Health checks
builder.Services.AddHealthChecks().AddDbContextCheck<DcdssContext>();

// DI: repositories (scoped), services (scoped), UoW, CurrentUserService, HttpContextAccessor
// IClinicalRuleStore + IReferenceRangeParser + ITokenService (singleton), IAdminSeeder (scoped)

var app = builder.Build();
app.UseMiddleware<GlobalExceptionMiddleware>();
if (!app.Environment.IsDevelopment()) { app.UseHsts(); }
app.UseHttpsRedirection();
app.UseCors("Default");
app.UseAuthentication();
app.UseAuthorization();
app.MapHealthChecks("/health");
app.MapControllers();
if (app.Environment.IsDevelopment()) { app.UseSwagger(); app.UseSwaggerUI(); }
using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<IAdminSeeder>().SeedAsync();
}
app.Run();
```

---

## Invariants & ownership summary
- Unique username/email: `AuthService` (friendly 409) + unique DB index (real owner).
- Enum closed sets: Data Annotation `EnumDataType` (input) + string-conversion Fluent config (storage).
- Allergy set per patient (3NF, unique per allergen): `PatientService` (writer) + `PatientAllergyConfiguration` unique index (real owner).
- Lab classification (incl. threshold-form → `Indeterminate`): `LabService` (only writer of non-`Ordered` `Status`).
- Medication status transitions: `MedicationService` (Active→Discontinued, idempotent, no revert).
- Critical prescription gate: `MedicationService` (calls `ISafetyCheckService`, throws `ConflictException` on Critical; `Allowed = !any Critical`).
- "DoctorId must be a Doctor" + future-date invariants: `MedicalRecordService`, `AppointmentService` (→ `BusinessRuleException` 400; service-enforced, not data annotations).
- Lab recency (latest result per test): `LabResultSelector.LatestPerTest` (pure helper, used by `CdsEngine` and `SafetyCheckService`).
- Cascade restrict + indexes: `IEntityTypeConfiguration<T>` files (data layer owns).
- Audit actor + persistence isolation + POPIA allowlist Details: `ICurrentUserService` + `AuditLogService` (IDbContextFactory) + `AuditActionFilter` (allowlist `Details`).
- JWT signing-key strength: `JwtSettingsValidator` (fail-fast on startup).

## Testability
- **Unit (no DB)**: `CdsRuleEvaluator` (input→findings with seeded rules), `ReferenceRangeParser` (strings→classification, incl. threshold-form→Indeterminate), `LabResultSelector.LatestPerTest`, `SafetyCheckService` (mocked repos + real `ClinicalRuleStore`), `AdminSeeder` (mocked repo; random vs configured password branches), `AuthService.Register`/`Login` (mocked `IUserRepository` + real `PasswordHasher`), `JwtSettingsValidator` (rejects placeholder/short keys), mapping/service logic for reports/patients/medications/appointments/medical-records (mocked repos).
- **Integration (need MySQL)**: end-to-end controller tests via `WebApplicationFactory` + a MySQL test DB (not available in this environment — implementer to provide). `dotnet build` passes regardless.
- All rule + parser + selector logic is pure and trivially unit-testable; the design intentionally isolates I/O behind interfaces so the business logic has no DB dependency.

---

## 17. Revision Responses (to `.agents/tasks/design-review.md`)

Each review finding is addressed below. Status: ✅ addressed in body, or justified.

| # | Severity | Finding | Response |
|---|---|---|---|
| H1 | HIGH | No endpoint creates a `MedicalRecord`; Lab/Medication infeasible | ✅ Added `POST /api/medical-records` (`MedicalRecordsController` + `IMedicalRecordService`/`MedicalRecordService` + `CreateMedicalRecordDto` + RBAC row Doctor/Nurse + audit `CREATE_MEDICAL_RECORD`). Service validates PatientId (404), DoctorId is a Doctor (400), VisitDate not in future (400); returns `201` with `MedicalRecordDto`. Unblocks `/api/laboratory/order` and `/api/medications`. (Sections 5.2, RBAC matrix, 15) |
| M2 | MEDIUM | `Patient.Allergies` delimited string violates 3NF | ✅ Replaced with a normalized `PatientAllergy` table (`PatientAllergyId` PK, `PatientId` FK Restrict, `Allergen` MaxLength 100, unique `(PatientId, Allergen)` index). Removed `Patient.Allergies`; added `PatientAllergy` entity + config + `IPatientAllergyRepository`; DTOs now use `IList<string>`; `PatientService` creates/replaces rows. (Sections 3, 4, 5.3, 6, 15) |
| M3 | MEDIUM | Audit `Details` would log PII (denylist) | ✅ `Details` is now a strict allowlist: `{ entityId, success }` (entity id from route `idArgument` or `IAuditableResult.EntityId` on the result DTO); login uses `{ username, success }`; never serializes the request DTO. Added `IAuditableResult` marker implemented by create-response DTOs. (Section 9) |
| M4 | MEDIUM | Default secrets committed; no fail-fast | ✅ `JwtSettingsValidator` (IValidateOptions) throws on startup if SigningKey is empty/equals `"REPLACE_ME"`/<32 bytes; base appsettings ships `"REPLACE_ME"` (rejected), dev override ships a usable dev key. `AdminSeed.Password` is `""` in base → seeder generates a random password and logs it once (Warning); dev override sets `Admin@12345`. No usable secret committed. (Sections 8, 10, 11) |
| M5 | MEDIUM | PHI read access not audited | ✅ `GET /api/patients/{id}` → `VIEW_PATIENT`; `GET /api/laboratory/patient/{patientId}` → `VIEW_LAB_HISTORY`, both decorated and audited (`Details = { id/patientId, success }`). List/report/audit reads remain unaudited (no individual record targeted) — documented in Section 5. (RBAC matrix + Section 9) |
| M6 | MEDIUM | Allergen matching ambiguous; ALG-002 two allergens | ✅ Allergen matching is exact, case-insensitive token membership (same rule as drug names). `ALG-002` split into `ALG-002a` (Allergen `"Sulfa"`) and `ALG-002b` (Allergen `"Sulfonamide"`), each a single-string `Allergen`. (Section 6) |
| M7 | MEDIUM | Lab recency inconsistent; stale; no dedup | ✅ Lab-driven rules evaluate the single most-recent result per `TestName` via pure `LabResultSelector.LatestPerTest`, used identically by `CdsEngine` and `SafetyCheckService`. CdsEngine merge dedups by normalized `TestName`; explicit input overrides stored latest for that test. (Section 6) |
| M8 | MEDIUM | `"<5"`/`">200"` misclassified as High/Low | ✅ `Classify` returns `Indeterminate` for threshold-form result values (does not compare the bound as the value); plain numbers classify normally. `ParseNumericValue` still parses threshold forms to their bound for `CriticalLabRule`/`DrugLabConflictRule` (intentional, clinically correct). (Section 7) |
| M9 | MEDIUM | Layering contradiction (Repository<T>/UoW in Services) | ✅ Moved `Repository.cs` and `UnitOfWork.cs` to `Repositories/` (Data); `IRepository<T>`/`IUnitOfWork` stay in `Services/Interfaces/` (Core). Concrete Data repos derive from a Data-layer base; Core depends only on abstractions. (Sections 1, 15) |
| N10 | NIT | Audit filter LOGIN switch + try/finally unspecified | ✅ Filter uses `try { executedContext = await next(); } finally { LogAsync(...); }`; `success = executedContext.Exception == null \|\| executedContext.ExceptionHandled`; `LOGIN` + `!success` → `LOGIN_FAILED`; exception propagates (ExceptionHandled not set). (Section 9) |
| N11 | NIT | `LabResultInput`, `SafetyCheckResult`, `SafetyConflict` undefined | ✅ All three defined with fields; added to file list (`Models/Cds/LabResultInput.cs`, `SafetyCheckResult.cs`, `SafetyConflict.cs`); `SafetyConflict→SafetyWarningDto` mapping specified; `Allowed = !any Critical`. (Sections 6, 15) |
| N12 | NIT | Pagination bound enforcement unspecified/inconsistent | ✅ Unified rule for all paged endpoints: clamp `page=max(1,page)`, `pageSize=clamp(pageSize,1,100)`; 400 only if non-int (model binding). Reports `month`/`year` are range-validated → 400 (not pagination). (Sections 5.3, 5.8) |
| N13 | NIT | "future" date validation described as annotation | ✅ Stated explicitly as service-enforced `BusinessRuleException` → 400 (no built-in `[Future]` attribute) for appointment date, medication start date, and medical-record visit date. (Sections 5.2, 5.6, 5.7) |
| N14 | NIT | Drug/allergen normalization unspecified | ✅ All comparisons use `Trim()` + `OrdinalIgnoreCase`; seed names canonical; README documents that prescribers must use canonical drug names (future controlled catalog). (Sections 6, 14) |
| N15 | NIT | `PasswordHash` MaxLength 256 tight | ✅ Changed to `MaxLength 512` in `User` entity + `UserConfiguration`. (Section 3) |
| N16 | NIT | `AddDbContext` + `AddDbContextFactory` both registered | ✅ Only `AddDbContextFactory<DcdssContext>` is registered (it also registers a scoped `DcdssContext` from the factory); `AddDbContext` dropped. (Sections 13, 16) |
| N17 | NIT | Role-claim mapping relies on default | ✅ `TokenValidationParameters.NameClaimType = "name"` and `RoleClaimType = "role"` set explicitly; token emits short-named `role`/`name` claims. No reliance on the default inbound map. (Section 10, 16) |
| N18 | NIT | UTC vs SAST for reports/appointments | ✅ Documented as a Known Limitation: UTC storage means SAST month boundaries and appointment display times can be off by up to 2 hours; time-zone-aware storage is backlog. (Sections 3, 5.8, 14) |
| N19 | NIT | No global authenticated-by-default fallback | ✅ `AddAuthorization(o => o.FallbackPolicy = RequireAuthenticatedUser())` added; login stays `[AllowAnonymous]`; role requirements stay per-action. (Sections 5, 16) |

All 19 findings are addressed; no finding is backlog'd or ignored.
