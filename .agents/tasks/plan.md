# DCDSS Backend Implementation Plan

**Design source:** `.agents/tasks/design.md` (revision 2, approved — all decisions final)
**Solution:** `c:\Users\nm\IdeaProjects\Digital Clinical Decision Support System (DCDSS)\DCDSS.sln`
**Project:** `c:\Users\nm\IdeaProjects\Digital Clinical Decision Support System (DCDSS)\DCDSS\`
**Build command:** `dotnet build "c:\Users\nm\IdeaProjects\Digital Clinical Decision Support System (DCDSS)\DCDSS.sln"`

> **Note:** This plan has been decomposed into FEAT artifacts at `.agents/tasks/task-dcdss-backend/`. The workflow implements each FEAT sequentially. This plan.md is the summary view used as fallback.

---

## Cross-cutting rules (apply to every file)

| Rule | Source |
|---|---|
| Root namespace: `DCDSS`; sub-namespaces aligned to folder | Design §1 |
| Entities: Fluent API only — no data annotations on entity classes | Design §3 |
| DTOs: data annotations only — no Fluent API | Design §5.x |
| All enums stored as strings: `.HasConversion<string>()` + `HasMaxLength` | Design §3 |
| All FK relationships: `.OnDelete(DeleteBehavior.Restrict)` | Design §4 |
| All DateTime: UTC (`DateTime.UtcNow`); `DateOnly` for DOB | Design §3 |
| `IRepository<T>` / `IUnitOfWork` in `Services/Interfaces/` (Core) | Design §1 |
| `Repository<T>` / `UnitOfWork` in `Repositories/` (Data) | Design §1, 15 |
| Only `AddDbContextFactory<DcdssContext>` — no `AddDbContext` | Design §13, §16 |
| `AuditLogService` uses `IDbContextFactory<DcdssContext>` for isolation | Design §9 |
| `GlobalExceptionMiddleware` registered FIRST in pipeline | Design §12 |
| FallbackPolicy = `RequireAuthenticatedUser` | Design §5 |
| `PasswordHash` MaxLength 512 | Design §3 |

---

## Layer 1 — Entities / Enums

**All in `DCDSS/Entities/`; namespace `DCDSS.Entities(.Enums)`**

### Enums — `Entities/Enums/`

- [ ] 1. **7 enum files** — create all in one step.
      - `UserRole.cs`: `{ Doctor, Nurse, LaboratoryStaff, Administrator }`
      - `Gender.cs`: `{ Male, Female, Other }`
      - `BloodType.cs`: `{ APositive, ANegative, BPositive, BNegative, ABPositive, ABNegative, OPositive, ONegative }`
      - `MedicalRecordStatus.cs`: `{ Active, Resolved, Archived }`
      - `LabResultStatus.cs`: `{ Ordered, Normal, High, Low, Indeterminate }`
      - `MedicationStatus.cs`: `{ Active, Discontinued }`
      - `AppointmentStatus.cs`: `{ Scheduled, Completed, Cancelled }`
      Files: `Entities/Enums/UserRole.cs`, `Gender.cs`, `BloodType.cs`, `MedicalRecordStatus.cs`, `LabResultStatus.cs`, `MedicationStatus.cs`, `AppointmentStatus.cs`
      Verify: `dotnet build` — 0 errors.

### Entity POCOs — `Entities/`

- [ ] 2. **8 entity files** — plain POCOs, no data annotations, navigation properties included.
      - `User.cs`: `int UserId`, `string Username`, `string PasswordHash`, `string FullName`, `string Email`, `UserRole Role`, `string? Department`, `DateTime CreatedAt`. Navigations: `ICollection<MedicalRecord> AuthoredMedicalRecords`, `ICollection<LabResult> OrderedLabResults`, `ICollection<Appointment> Appointments`, `ICollection<AuditLog> AuditLogs`.
      - `Patient.cs`: `int PatientId`, `string FirstName`, `string Surname`, `Gender Gender`, `DateOnly DateOfBirth`, `string Address`, `string Phone`, `BloodType BloodType`, `string? EmergencyContact`, `DateTime CreatedAt`. Navigations: `ICollection<MedicalRecord> MedicalRecords`, `ICollection<Appointment> Appointments`, `ICollection<PatientAllergy> Allergies`.
      - `PatientAllergy.cs` (**NEW** — 8th entity): `int PatientAllergyId`, `int PatientId`, `string Allergen`. Navigation: `Patient Patient`. Unique composite index `(PatientId, Allergen)` — enforced in config.
      - `MedicalRecord.cs`: `int RecordId`, `int PatientId`, `int DoctorId`, `string Diagnosis`, `string? TreatmentNotes`, `DateTime VisitDate`, `MedicalRecordStatus Status`. Navigations: `Patient Patient`, `User Doctor`, `ICollection<LabResult> LabResults`, `ICollection<Medication> Medications`.
      - `LabResult.cs`: `int ResultId`, `int RecordId`, `string TestName`, `string? ResultValue`, `string ReferenceRange`, `LabResultStatus Status`, `int OrderedBy`, `DateTime DateOrdered`. Navigations: `MedicalRecord Record`, `User OrderedByUser`.
      - `Medication.cs`: `int MedicationId`, `int RecordId`, `string DrugName`, `string Dosage`, `string Frequency`, `DateTime StartDate`, `MedicationStatus Status`. Navigation: `MedicalRecord Record`.
      - `Appointment.cs`: `int AppointmentId`, `int PatientId`, `int DoctorId`, `DateTime AppointmentDate`, `AppointmentStatus Status`. Navigations: `Patient Patient`, `User Doctor`.
      - `AuditLog.cs`: `int AuditId`, `int? UserId` (**nullable** — captures unauthenticated events), `string Action`, `string? Details`, `DateTime Timestamp`. Navigation: `User? User`.
      Files: `Entities/User.cs`, `Patient.cs`, `PatientAllergy.cs`, `MedicalRecord.cs`, `LabResult.cs`, `Medication.cs`, `Appointment.cs`, `AuditLog.cs`
      Verify: `dotnet build` — 0 errors.

---

## Layer 2 — Data / Configurations

- [ ] 3. **`Data/DcdssContext.cs`** — `DbContext` subclass. 8 `DbSet<T>` properties (`Users`, `Patients`, `PatientAllergies`, `MedicalRecords`, `LabResults`, `Medications`, `Appointments`, `AuditLogs`). `OnModelCreating`: call `modelBuilder.ApplyConfigurationsFromAssembly(typeof(DcdssContext).Assembly)`. No manual `HasOne`/`HasMany` calls here — all in config files.
      Files: `Data/DcdssContext.cs`
      Verify: `dotnet build` — 0 errors.

- [ ] 4. **8 Fluent API configuration files** — `IEntityTypeConfiguration<T>`, one per entity, in `Data/Configurations/`.

      **Shared rules for all 8:**
      - PK: `.HasKey(e => e.XxxId)` + `.ValueGeneratedOnAdd()`.
      - Column names: `.HasColumnName("XxxID")` (schema columns use ALL-CAP suffix ID).
      - Enums: `.HasConversion<string>().HasMaxLength(N)`.
      - All FKs: `.OnDelete(DeleteBehavior.Restrict)`.
      - `CreatedAt` / `DateOrdered` / `StartDate` / `Timestamp`: `.HasDefaultValueSql("CURRENT_TIMESTAMP(6)")`.

      **`UserConfiguration.cs`**: Username unique index, Email unique index, PasswordHash MaxLength **512**.

      **`PatientConfiguration.cs`**: `DateOfBirth` column name `DOB`; `DateOnly` maps to MySQL `DATE` automatically via Pomelo 8.x.

      **`PatientAllergyConfiguration.cs`**: composite unique index `(PatientId, Allergen)` + individual index on `PatientId`; FK to `Patients` Restrict.

      **`MedicalRecordConfiguration.cs`**: indexes on `PatientId`, `DoctorId`, `VisitDate`; Status default `Active`.

      **`LabResultConfiguration.cs`**: indexes on `RecordId`, `OrderedBy`, `DateOrdered`; Status default `Ordered`; `ResultValue` is optional MaxLength 50.

      **`MedicationConfiguration.cs`**: index on `RecordId`; Status default `Active`.

      **`AppointmentConfiguration.cs`**: indexes on `PatientId`, `DoctorId`, `AppointmentDate`; Status default `Scheduled`.

      **`AuditLogConfiguration.cs`**: `UserId` is optional (`IsRequired(false)`); FK to Users Restrict; indexes on `UserId`, `Timestamp`.

      Files: `Data/Configurations/UserConfiguration.cs`, `PatientConfiguration.cs`, `PatientAllergyConfiguration.cs`, `MedicalRecordConfiguration.cs`, `LabResultConfiguration.cs`, `MedicationConfiguration.cs`, `AppointmentConfiguration.cs`, `AuditLogConfiguration.cs`
      Verify: `dotnet build` — 0 errors.

---

## Layer 3 — Exceptions

- [ ] 5. **3 domain exception classes** in `Exceptions/`.
      - `NotFoundException.cs`: inherits `Exception`, ctor `(string message)`.
      - `ConflictException.cs`: same shape.
      - `BusinessRuleException.cs`: same shape.
      Files: `Exceptions/NotFoundException.cs`, `ConflictException.cs`, `BusinessRuleException.cs`
      Verify: `dotnet build` — 0 errors.

---

## Layer 4 — Repository Abstractions (Core)

- [ ] 6. **`IRepository<T>` and `IUnitOfWork`** — in `Services/Interfaces/` (Core layer, not Repositories/).
      - `IRepository<T> where T : class`: `GetByIdAsync(int id)`, `GetAllAsync()`, `AddAsync(T entity)`, `Update(T entity)`, `Remove(T entity)`, `IQueryable<T> Query()`.
      - `IUnitOfWork`: `Task<int> SaveChangesAsync(CancellationToken ct = default)`.
      Files: `Services/Interfaces/IRepository.cs`, `Services/Interfaces/IUnitOfWork.cs`
      Verify: `dotnet build` — 0 errors.

- [ ] 7. **8 specific repository interfaces** in `Repositories/Interfaces/`.
      - `IUserRepository`: extends `IRepository<User>`. Extra: `GetByUsernameAsync(string)`, `GetByEmailAsync(string)`.
      - `IPatientRepository`: extends `IRepository<Patient>`. Extra: `SearchAsync(page, pageSize, search)` → `(IList<Patient>, int)`, `GetByIdWithHistoryAsync(int)`.
      - `IPatientAllergyRepository`: does **NOT** extend `IRepository<T>`. Methods: `GetByPatientAsync(int)`, `DeleteByPatientAsync(int)`, `AddAsync(PatientAllergy)`. (no generic base — purpose-built for allergy set operations)
      - `IMedicalRecordRepository`: extends `IRepository<MedicalRecord>`. No extra methods (base `GetByIdAsync` is sufficient).
      - `ILabResultRepository`: extends `IRepository<LabResult>`. Extra: `GetByPatientAsync(int patientId)`.
      - `IMedicationRepository`: extends `IRepository<Medication>`. Extra: `GetActiveByPatientAsync(int patientId)`.
      - `IAppointmentRepository`: extends `IRepository<Appointment>`. Extra: `GetByDoctorAsync(int doctorId)`.
      - `IAuditLogRepository`: extends `IRepository<AuditLog>`. Extra: `GetPagedAsync(int page, int pageSize)` → `(IList<AuditLog>, int)`.
      Files: `Repositories/Interfaces/IUserRepository.cs`, `IPatientRepository.cs`, `IPatientAllergyRepository.cs`, `IMedicalRecordRepository.cs`, `ILabResultRepository.cs`, `IMedicationRepository.cs`, `IAppointmentRepository.cs`, `IAuditLogRepository.cs`
      Verify: `dotnet build` — 0 errors.

---

## Layer 5 — Repository Implementations (Data)

- [ ] 8. **`Repository<T>` base + `UnitOfWork`** in `Repositories/` (Data layer).
      - `Repository<T>`: ctor `(DcdssContext context)`. Implements all `IRepository<T>` methods via `_context.Set<T>()`.
      - `UnitOfWork`: ctor `(DcdssContext context)`. `SaveChangesAsync` → `_context.SaveChangesAsync(ct)`.
      Files: `Repositories/Repository.cs`, `Repositories/UnitOfWork.cs`
      Verify: `dotnet build` — 0 errors.

- [ ] 9. **8 specific repository implementations** in `Repositories/`.
      All inherit `Repository<T>` except `PatientAllergyRepository` (which implements `IPatientAllergyRepository` directly).

      **`PatientRepository`**: `SearchAsync` — `IQueryable` with optional `Contains` filter on `FirstName`/`Surname`, count first then paginate. `GetByIdWithHistoryAsync` — multi-level `Include`: `Allergies`, `MedicalRecords.LabResults`, `MedicalRecords.Medications`, `MedicalRecords.Doctor`.

      **`PatientAllergyRepository`**: holds `DcdssContext` directly (no generic base). `DeleteByPatientAsync` loads rows and calls `RemoveRange`. Each `AddAsync` call must be followed by `SaveChangesAsync` via `IUnitOfWork` in the service — the repo just stages the add.

      **`LabResultRepository`**: `GetByPatientAsync` — join through `MedicalRecord.PatientId`, include `Record`, order by `DateOrdered DESC`.

      **`MedicationRepository`**: `GetActiveByPatientAsync` — filter `Status == Active`, join through `Record.PatientId`, include `Record`.

      **`AppointmentRepository`**: `GetByDoctorAsync` — include `Patient`, `Doctor`, order by `AppointmentDate ASC`.

      **`AuditLogRepository`**: `GetPagedAsync` — include `User`, order by `Timestamp DESC`, count then skip/take.

      Files: `Repositories/UserRepository.cs`, `PatientRepository.cs`, `PatientAllergyRepository.cs`, `MedicalRecordRepository.cs`, `LabResultRepository.cs`, `MedicationRepository.cs`, `AppointmentRepository.cs`, `AuditLogRepository.cs`
      Verify: `dotnet build` — 0 errors.

---

## Layer 6 — Models (non-entity domain)

- [ ] 10. **`Models/ReferenceRangeParseResult.cs`** — `{ double? Min, double? Max, string? Unit }`. Namespace `DCDSS.Models`.
        File: `Models/ReferenceRangeParseResult.cs`

- [ ] 11. **14 CDS model files** in `Models/Cds/`. Namespace `DCDSS.Models.Cds`.
      - Enums: `CdsSeverity { Info, Warning, High, Critical }`, `CdsFindingType { Diagnostic, Interaction, Allergy, CriticalLab, DrugLab }`.
      - Rule models: `DiagnosticRule`, `DrugInteractionPair`, `AllergyConflict`, `CriticalLabRule`, `DrugLabConflictRule` — all plain record/class types.
      - Finding/result models: `CdsFinding`, `CdsEvaluationInput`, `CdsEvaluationResult`, `LabResultInput`, `SafetyConflict`, `SafetyCheckResult`.
      - Pure helper: **`LabResultSelector`** — `public static class LabResultSelector { public static IList<LabResult> LatestPerTest(IEnumerable<LabResult> results) => results.GroupBy(r => r.TestName, StringComparer.OrdinalIgnoreCase).Select(g => g.OrderByDescending(r => r.DateOrdered).First()).ToList(); }`. This is the single implementation of the "latest result per test" rule used by both `CdsEngine` and `SafetyCheckService`.
      Files: 14 files in `Models/Cds/`
      Verify: `dotnet build` — 0 errors.

---

## Layer 7 — DTOs

- [ ] 12. **Common DTOs** — `DTOs/Common/`.
      - `PagedResult<T>`: `Items`, `Total`, `Page`, `PageSize`.
      - `IAuditableResult`: `int? EntityId { get; }` — marker interface implemented by all create-response DTOs.
      Files: `DTOs/Common/PagedResult.cs`, `DTOs/Common/IAuditableResult.cs`

- [ ] 13. **Auth DTOs** — `DTOs/Auth/`. Namespace `DCDSS.DTOs.Auth`.
      - `RegisterDto`: all fields with data annotations per design §5.1.
      - `LoginDto`: `Username [Required, MaxLength(50)]`, `Password [Required]`.
      - `UserDto`: all user fields except `PasswordHash`. **Implements `IAuditableResult`: `EntityId => UserId`**.
      - `LoginResponseDto`: `Token`, `Username`, `Role`, `ExpiresAt`.
      Files: `DTOs/Auth/RegisterDto.cs`, `LoginDto.cs`, `UserDto.cs`, `LoginResponseDto.cs`

- [ ] 14. **MedicalRecords DTOs** — `DTOs/MedicalRecords/`. (**NEW module**)
      - `CreateMedicalRecordDto`: `PatientId [Required]`, `DoctorId [Required]`, `Diagnosis [Required, MaxLength(500)]`, `TreatmentNotes [MaxLength(2000)]`, `VisitDate [Required]`.
      - `MedicalRecordDto`: `RecordId`, `PatientId`, `DoctorId`, `DoctorName?`, `Diagnosis`, `TreatmentNotes?`, `VisitDate`, `Status`, `LabResults: IList<LabResultDto>`, `Medications: IList<MedicationDto>`. **Implements `IAuditableResult`: `EntityId => RecordId`**. Gotcha: references `LabResultDto` and `MedicationDto` from other namespaces.
      Files: `DTOs/MedicalRecords/CreateMedicalRecordDto.cs`, `MedicalRecordDto.cs`

- [ ] 15. **Lab DTOs** — `DTOs/Laboratory/`.
      - `OrderLabTestDto`: `RecordId [Required]`, `TestName [Required, MaxLength(100)]`, `ReferenceRange [Required, MaxLength(100)]`.
      - `UpdateLabResultDto`: `ResultValue [Required, MaxLength(50)]`, `ReferenceRange? [MaxLength(100)]`.
      - `LabResultDto`: all result fields. **Implements `IAuditableResult`: `EntityId => ResultId`**.
      Files: `DTOs/Laboratory/OrderLabTestDto.cs`, `UpdateLabResultDto.cs`, `LabResultDto.cs`

      > Create Lab DTOs **before** MedicalRecordDto (step 14 references `LabResultDto`). Adjust order if compiling step 14 after step 15 is simpler.

- [ ] 16. **Medication DTOs** — `DTOs/Medications/`.
      - `CreateMedicationDto`: `RecordId [Required]`, `DrugName [Required, MaxLength(100)]`, `Dosage [Required, MaxLength(50)]`, `Frequency [Required, MaxLength(50)]`, `StartDate?` (no annotation — service validates not in future).
      - `MedicationDto`: all fields. **Implements `IAuditableResult`: `EntityId => MedicationId`**.
      - `SafetyWarningDto`: `RuleId`, `Type`, `Severity`, `Message`, `RelatedDrug?`, `RelatedAllergen?`.
      - `PrescribeMedicationResponseDto`: `Medication: MedicationDto`, `Warnings: IList<SafetyWarningDto>`. **Implements `IAuditableResult`: `EntityId => Medication?.MedicationId`** (the audit filter reads `EntityId` from the `ObjectResult.Value`; this DTO wraps the medication).
      Files: `DTOs/Medications/CreateMedicationDto.cs`, `MedicationDto.cs`, `SafetyWarningDto.cs`, `PrescribeMedicationResponseDto.cs`

- [ ] 17. **Patients DTOs** — `DTOs/Patients/`.
      - `CreatePatientDto`/`UpdatePatientDto`: identical. Phone regex: `^[0-9+()\-\s]{7,20}$`. Allergies: `IList<string>?` (no list-item annotation — service trims/deduplicates).
      - `PatientSummaryDto`: basic fields.
      - `PatientDetailDto`: all patient fields + `IList<string> Allergies` + `IList<MedicalRecordDto> MedicalHistory`. **Implements `IAuditableResult`: `EntityId => PatientId`**.
      Files: `DTOs/Patients/CreatePatientDto.cs`, `UpdatePatientDto.cs`, `PatientSummaryDto.cs`, `PatientDetailDto.cs`

- [ ] 18. **Appointment DTOs** — `DTOs/Appointments/`.
      - `CreateAppointmentDto`: `PatientId [Required]`, `DoctorId [Required]`, `AppointmentDate [Required]`.
      - `AppointmentDto`: all fields + `PatientName?`, `DoctorName?`. **Implements `IAuditableResult`: `EntityId => AppointmentId`**.
      Files: `DTOs/Appointments/CreateAppointmentDto.cs`, `AppointmentDto.cs`

- [ ] 19. **CDS DTOs** — `DTOs/Cds/`.
      - `CdsEvaluationRequestDto`: `int? PatientId`, `IList<string>? Symptoms`, `IList<LabResultInputDto>? LabResults`, `IList<MedicationInputDto>? Medications`, `IList<string>? Allergies`.
      - `LabResultInputDto`: `TestName [Required]`, `ResultValue [Required]`, `ReferenceRange [Required]`.
      - `MedicationInputDto`: `DrugName [Required]`, `Dosage?`.
      - `CdsFindingDto`: `RuleId`, `Type`, `Severity`, `Title`, `Description`, `RelatedItem?`.
      - `CdsEvaluationResponseDto`: `DiagnosticRecommendations`, `InteractionWarnings`, `CriticalAlerts` (all `IList<CdsFindingDto>`), `EvaluatedAt`.
      Files: `DTOs/Cds/CdsEvaluationRequestDto.cs`, `LabResultInputDto.cs`, `MedicationInputDto.cs`, `CdsFindingDto.cs`, `CdsEvaluationResponseDto.cs`

- [ ] 20. **Reports/Audit DTOs** — `DTOs/Reports/` and `DTOs/Audit/`.
      - `MonthlyStatsDto`: `Month`, `Year`, `TotalVisits`, `NewPatientRegistrations`, `LabTestsOrdered`, `DepartmentPerformance: IList<DepartmentStatDto>`.
      - `DepartmentStatDto`: `Department`, `VisitCount`.
      - `AuditLogDto`: `AuditId`, `UserId?`, `Username?`, `Action`, `Details?`, `Timestamp`.
      Files: `DTOs/Reports/MonthlyStatsDto.cs`, `DepartmentStatDto.cs`, `DTOs/Audit/AuditLogDto.cs`

      Verify steps 12–20: `dotnet build` — 0 errors.

---

## Layer 8 — Service Interfaces (Core)

- [ ] 21. **16 service interface files** in `Services/Interfaces/`.
      All interfaces match the method signatures in design §5.x, §6, §8–10. Key signatures:
      - `IAuthService`: `RegisterAsync(RegisterDto) → UserDto`, `LoginAsync(LoginDto) → LoginResponseDto`.
      - `ITokenService`: `GenerateToken(User user) → string`.
      - `IPatientService`: `GetPagedAsync`, `GetByIdWithHistoryAsync`, `CreateAsync`, `UpdateAsync`.
      - `IMedicalRecordService`: `CreateAsync(CreateMedicalRecordDto) → MedicalRecordDto`.
      - `ICdsEngine`: `EvaluateAsync(CdsEvaluationRequestDto) → CdsEvaluationResponseDto`.
      - `ICdsRuleEvaluator`: `Evaluate(CdsEvaluationInput) → CdsEvaluationResult` (pure — no `async`).
      - `ILabService`: `OrderAsync(OrderLabTestDto, int orderedByUserId)`, `UpdateResultAsync(int id, UpdateLabResultDto)`, `GetByPatientAsync(int patientId)`.
      - `IMedicationService`: `PrescribeAsync(CreateMedicationDto, int currentUserId)`, `DiscontinueAsync(int id)`.
      - `IAppointmentService`: `CreateAsync`, `GetByDoctorAsync`.
      - `IReportService`: `GetMonthlyStatsAsync(int month, int year)`.
      - `IAuditLogService`: `LogAsync(int? userId, string action, string details)`.
      - `ISafetyCheckService`: `CheckPrescriptionAsync(int patientId, string drugName, int? excludeMedicationId) → SafetyCheckResult`.
      - `IClinicalRuleStore`: 5 `Get*()` methods returning rule lists.
      - `IReferenceRangeParser`: `ParseRange`, `ParseNumericValue`, `Classify`.
      - `IAdminSeeder`: `SeedAsync()`.
      - `ICurrentUserService`: `GetUserId() → int?`, `GetRole() → string?`, `GetUsername() → string?`. (Also has its implementation in `Security/` — see step 22.)
      Files: 16 files in `Services/Interfaces/`
      Verify: `dotnet build` — 0 errors.

---

## Layer 9 — Configuration + Security

- [ ] 22. **Typed settings classes + validator** in `Configuration/`. Namespace `DCDSS.Configuration`.
      - `JwtSettings.cs`: `Issuer`, `Audience`, `SigningKey`, `ExpiryMinutes`.
      - `CorsSettings.cs`: `AllowedOrigins` (comma-separated string).
      - `AdminSeedSettings.cs`: `Username`, `Password`, `FullName`, `Email`, `Department?`.
      - **`JwtSettingsValidator.cs`**: implements `IValidateOptions<JwtSettings>`. Rejects if `SigningKey` is null/empty/whitespace, equals `"REPLACE_ME"`, or UTF-8 byte count < 32. Returns `ValidateOptionsResult.Fail(...)` in those cases; else `Success`. This is the fail-fast gate.
      Files: `Configuration/JwtSettings.cs`, `CorsSettings.cs`, `AdminSeedSettings.cs`, `JwtSettingsValidator.cs`

- [ ] 23. **`Security/CurrentUserService.cs`** — namespace `DCDSS.Security`. Implements `ICurrentUserService`. Constructor `(IHttpContextAccessor accessor)`. `GetUserId()` → parse `User.FindFirst("sub")?.Value` as `int?`. `GetRole()` → `User.FindFirst("role")?.Value`. `GetUsername()` → `User.FindFirst("name")?.Value`.
      File: `Security/CurrentUserService.cs`
      Verify: `dotnet build` — 0 errors.

---

## Layer 10 — Service Implementations

- [ ] 24. **`Services/ReferenceRangeParser.cs`** — implements `IReferenceRangeParser`. Singleton. Pure/stateless.

      **`ParseNumericValue(string)`:** if value matches `^[<>]\s*([\d.]+)` → parse capture group 1. Else `double.TryParse(trimmed)`. Else `null`.

      **`ParseRange(string)`:** three regex patterns tried in order:
        1. `^([\d.]+)\s*-\s*([\d.]+)(?:\s*(.+))?$` → Min + Max + optional Unit.
        2. `^<\s*([\d.]+)(?:\s*(.+))?$` → Max only + optional Unit.
        3. `^>\s*([\d.]+)(?:\s*(.+))?$` → Min only + optional Unit.
        4. Fallback → `{ Min=null, Max=null, Unit=null }`.

      **`Classify(string resultValue, string referenceRange)`:**
        1. If `Regex.IsMatch(resultValue.Trim(), @"^[<>]\s*\d")` → `Indeterminate`. (**Critical: threshold-form values must return Indeterminate — never classify `<5` as Low or High.**)
        2. `val = ParseNumericValue(resultValue)`. If null → `Indeterminate`.
        3. `parsed = ParseRange(referenceRange)`.
           - Both Min and Max: `val < Min → Low`, `val > Max → High`, else `Normal`.
           - Max only: `val >= Max → High`, else `Normal`.
           - Min only: `val <= Min → Low`, else `Normal`.
           - No bounds → `Indeterminate`.
      File: `Services/ReferenceRangeParser.cs`

- [ ] 25. **`Services/ClinicalRuleStore.cs`** — implements `IClinicalRuleStore`. Singleton. Static seed data.

      Exact seed values from design §6:
      - DiagnosticRules: DIA-001 (polyuria/polydipsia/weight loss/fatigue, min 2, Diabetes, High), DIA-002 (fever/cough/dyspnea, min 2, respiratory, Warning), DIA-003 (chest pain/dyspnea/diaphoresis, min 2, ACS, Critical).
      - DrugInteractionPairs: INT-001 (Warfarin+Aspirin, Critical), INT-002 (Warfarin+Ibuprofen, Critical), INT-003 (Enalapril+Spironolactone, High), INT-004 (Simvastatin+Clarithromycin, High).
      - AllergyConflicts: ALG-001 (Penicillin allergen, drugs=[Penicillin,Amoxicillin,Ampicillin,Amoxiclav,Flucloxacillin], Critical), ALG-002a (Sulfa allergen, drugs=[Sulfamethoxazole,Co-trimoxazole,Trimethoprim], Critical), ALG-002b (Sulfonamide allergen, same drugs, Critical), ALG-003 (Aspirin allergen, drugs=[Aspirin], High).
      - CriticalLabRules: LAB-001 (Glucose, low=2.5, high=22, Critical), LAB-002 (Potassium, low=2.5, high=6.5, Critical), LAB-003 (Hemoglobin, low=6, high=20, Critical).
      - DrugLabConflictRules: DLC-001 (Metformin, Creatinine, >, 130, Critical), DLC-002 (Ibuprofen, Creatinine, >, 120, High), DLC-003 (Warfarin, INR, >, 4, Critical).

      Gotcha: `AllergyConflict.Allergen` is a **single string** — ALG-002 is deliberately split into ALG-002a and ALG-002b for "Sulfa" and "Sulfonamide" respectively.
      File: `Services/ClinicalRuleStore.cs`

- [ ] 26. **`Services/CdsRuleEvaluator.cs`** — implements `ICdsRuleEvaluator`. Pure (no I/O). Constructor `(IClinicalRuleStore ruleStore, IReferenceRangeParser parser)`.

      Evaluate pipeline (all comparisons: `string.Equals(a.Trim(), b.Trim(), OrdinalIgnoreCase)`):
      - **Diagnostic**: count `TriggerSymptoms` contained in `input.Symptoms`; fire if `count >= MinSymptomMatches`.
      - **Drug-drug**: check both `(DrugA,DrugB)` orderings against normalized medication list.
      - **Allergy**: exact OrdinalIgnoreCase match on `Allergen` in `input.Allergies`; any of `rule.Drugs` in medications.
      - **Critical lab**: look up `LatestLabsDict` (keyed by TestName, OrdinalIgnoreCase); parse numeric value; compare against `CriticalLow`/`CriticalHigh`.
      - **Drug-lab**: drug in medications (exact); look up lab by `LabTest` (OrdinalIgnoreCase `Contains`); compare with `Operator`.
      File: `Services/CdsRuleEvaluator.cs`

- [ ] 27. **`Services/SafetyCheckService.cs`** — implements `ISafetyCheckService`. Constructor `(IClinicalRuleStore ruleStore, ILabResultRepository labResultRepo, IMedicationRepository medicationRepo, IPatientAllergyRepository allergyRepo, IReferenceRangeParser parser)`.

      `CheckPrescriptionAsync(int patientId, string drugName, int? excludeMedicationId)`:
      1. Load active medications, excluding `excludeMedicationId` if provided.
      2. Load patient allergies.
      3. Load lab results → `LabResultSelector.LatestPerTest(...)`.
      4. Run: drug-drug interactions, drug-allergy conflicts, drug-lab conflicts (same logic as evaluator but against stored data).
      5. `Allowed = !conflicts.Any(c => c.Severity == CdsSeverity.Critical)`.
      File: `Services/SafetyCheckService.cs`

- [ ] 28. **`Services/CdsEngine.cs`** — implements `ICdsEngine`. Constructor `(IPatientAllergyRepository allergyRepo, IMedicationRepository medicationRepo, ILabResultRepository labResultRepo, ICdsRuleEvaluator evaluator)`.

      `EvaluateAsync` merge logic:
      - If `PatientId` provided: load stored allergies, active med DrugNames, lab results → `LatestPerTest`.
      - Build `Dictionary<string, LabResultInput>` keyed OrdinalIgnoreCase from stored latest. Overlay explicit `request.LabResults` (explicit wins per key).
      - Medications: union stored + explicit, dedup OrdinalIgnoreCase Trim.
      - Allergies: union stored + explicit, dedup OrdinalIgnoreCase Trim.
      - Symptoms: explicit only, dedup.
      - Call evaluator → map findings to `CdsEvaluationResponseDto`.
      - Mapping: `Diagnostic` type → `DiagnosticRecommendations`; `Interaction`/`DrugLab` → `InteractionWarnings`; `Allergy`/`CriticalLab` → `CriticalAlerts`.
      File: `Services/CdsEngine.cs`

- [ ] 29. **`Services/TokenService.cs`** — implements `ITokenService`. Singleton. Constructor `(IOptions<JwtSettings> opts)`.
      Claims: `sub=UserId.ToString()`, `role=Role.ToString()`, `name=Username`. Algorithm: `HmacSha256`. Uses `JwtSecurityTokenHandler`.
      File: `Services/TokenService.cs`

- [ ] 30. **`Services/AuthService.cs`** — implements `IAuthService`. Constructor `(IUserRepository userRepo, IUnitOfWork uow, PasswordHasher<User> hasher, ITokenService tokenService)`.
      - `RegisterAsync`: check uniqueness (throw `ConflictException` if dup username or email), hash, create entity, save, return `UserDto`.
      - `LoginAsync`: fetch by username → if null throw `UnauthorizedAccessException("Invalid username or password.")`. Verify hash → if fail throw same. Generate token. Return `LoginResponseDto`.
      Gotcha: **never reveal which field failed** — same message for both cases.
      File: `Services/AuthService.cs`

- [ ] 31. **`Services/PatientService.cs`** — implements `IPatientService`. Constructor `(IPatientRepository patientRepo, IPatientAllergyRepository allergyRepo, IUnitOfWork uow)`.
      - `GetPagedAsync`: clamp page/pageSize, call `SearchAsync`, map to `PatientSummaryDto`.
      - `CreateAsync`: validate DOB not > today (`DateOnly.FromDateTime(DateTime.UtcNow)`) → `BusinessRuleException` if future. Save patient. Then save allergy rows: trim, dedup case-insensitively, skip empty.
      - `UpdateAsync`: throw `NotFoundException` if not found. Update fields. Delete existing allergies via `DeleteByPatientAsync`. Re-insert normalized new set. Save.
      Allergy dedup rule: case-insensitive, trim both ends, drop empty entries.
      File: `Services/PatientService.cs`

- [ ] 32. **`Services/MedicalRecordService.cs`** — implements `IMedicalRecordService`. Constructor `(IMedicalRecordRepository recordRepo, IRepository<Patient> patientRepo, IRepository<User> userRepo, IUnitOfWork uow)`.
      Three validation checks → `NotFoundException` / `BusinessRuleException`:
      1. Patient exists.
      2. Doctor user exists **and** `Role == Doctor`.
      3. `VisitDate` ≤ `DateTime.UtcNow`.
      File: `Services/MedicalRecordService.cs`

- [ ] 33. **`Services/LabService.cs`** — implements `ILabService`. Constructor `(ILabResultRepository labRepo, IMedicalRecordRepository recordRepo, IUnitOfWork uow, IReferenceRangeParser parser)`.
      - `OrderAsync`: validate record exists (404). Create with `Status=Ordered`, `ResultValue=null`.
      - `UpdateResultAsync`: validate result exists. Set `ResultValue`. Optionally update `ReferenceRange`. `Status = parser.Classify(ResultValue, ReferenceRange)`. Save.
      - `GetByPatientAsync`: retrieve via repo, map to DTOs.
      Invariant: **only `UpdateResultAsync` transitions `Status` out of `Ordered`** — `OrderAsync` always sets `Ordered`.
      File: `Services/LabService.cs`

- [ ] 34. **`Services/MedicationService.cs`** — implements `IMedicationService`. Constructor `(IMedicationRepository medicationRepo, IMedicalRecordRepository recordRepo, ISafetyCheckService safetyService, IUnitOfWork uow)`.
      - `PrescribeAsync`: resolve `PatientId` via record. Run safety check. If `!Allowed` → throw `ConflictException` with concatenated critical conflict messages. Validate `StartDate` not in future (if provided). Create medication. Warnings = non-Critical conflicts → `SafetyWarningDto`.
      - `DiscontinueAsync`: load → `NotFoundException` if missing. Set `Discontinued`. Idempotent.
      File: `Services/MedicationService.cs`

- [ ] 35. **`Services/AppointmentService.cs`** — implements `IAppointmentService`. Constructor `(IAppointmentRepository apptRepo, IRepository<Patient> patientRepo, IRepository<User> userRepo, IUnitOfWork uow)`.
      - `CreateAsync`: patient exists (404), doctor exists and `Role==Doctor` (400), `AppointmentDate > DateTime.UtcNow` (400 if not).
      - `GetByDoctorAsync`: validate doctor exists and is Doctor (400 if not).
      File: `Services/AppointmentService.cs`

- [ ] 36. **`Services/ReportService.cs`** — implements `IReportService`. Constructor `(IRepository<MedicalRecord> recordRepo, IRepository<Patient> patientRepo, IRepository<LabResult> labRepo)`.
      Range validation: month must be 1–12, year must be ≥ 2000 → `BusinessRuleException` if out of range.
      Queries: LINQ using `.Query()` + EF `Where`/`GroupBy`/`Count`/`ToListAsync`. `DepartmentPerformance` groups by `Doctor.Department ?? "Unassigned"`.
      File: `Services/ReportService.cs`

- [ ] 37. **`Services/AuditLogService.cs`** — implements `IAuditLogService`. Constructor `(IDbContextFactory<DcdssContext> factory, ILogger<AuditLogService> logger)`.
      `LogAsync`: create isolated `DcdssContext` via factory, add `AuditLog`, save. Catch all exceptions and log at Error — **never rethrow** (audit must not fail the request).
      File: `Services/AuditLogService.cs`

- [ ] 38. **`Services/AdminSeeder.cs`** — implements `IAdminSeeder`. Constructor `(IUserRepository userRepo, PasswordHasher<User> hasher, IOptions<AdminSeedSettings> settings, ILogger<AdminSeeder> logger, IUnitOfWork uow)`.
      - If any Administrator user exists → return (idempotent).
      - If `Password` is null/empty/whitespace → generate 16-char cryptographically random alphanumeric via `RandomNumberGenerator`. Log at Warning with the generated password (one-time bootstrap).
      - Hash password. Create User. Save.
      File: `Services/AdminSeeder.cs`

      Verify steps 24–38: `dotnet build` — 0 errors.

---

## Layer 11 — Middleware

- [ ] 39. **`Middleware/AuditActionAttribute.cs`** — `[AttributeUsage(AttributeTargets.Method)]`. Properties: `string ActionName`, `string? IdArgument`. Constructor `(string actionName, string? idArgument = null)`.
      File: `Middleware/AuditActionAttribute.cs`

- [ ] 40. **`Middleware/GlobalExceptionMiddleware.cs`** — `RequestDelegate` + `ILogger` in ctor. `InvokeAsync` wraps `_next(ctx)` in try/catch. Maps exceptions to RFC 7807 `ProblemDetails` JSON:

      | Exception | Status | Title |
      |---|---|---|
      | `NotFoundException` | 404 | "Not Found" |
      | `ConflictException` | 409 | "Conflict" |
      | `BusinessRuleException` | 400 | "Business Rule Violation" |
      | `UnauthorizedAccessException` | 401 | "Unauthorized" |
      | `DbUpdateException` | 409 | "Data Conflict" |
      | Any other | 500 | "Internal Server Error" |

      In Development: include `ex.Message` in `Detail`. In production: omit. Never include stack trace in response (log only). Include `traceId` extension. `Content-Type: application/problem+json`.
      File: `Middleware/GlobalExceptionMiddleware.cs`

- [ ] 41. **`Middleware/AuditActionFilter.cs`** — `IAsyncActionFilter`. Constructor `(IAuditLogService auditService, ICurrentUserService currentUser)`.

      **Critical implementation notes:**
      - Wrap `await next()` in `try { } finally { }` — audit runs even when action throws.
      - `success = executedCtx.Exception == null || executedCtx.ExceptionHandled`.
      - **Do NOT set `ExceptionHandled = true`** — let `GlobalExceptionMiddleware` handle the exception.
      - `LOGIN` + `!success` → emit `"LOGIN_FAILED"`.
      - Details allowlist (never serialize request DTO body):
        - `LOGIN`/`LOGIN_FAILED` → `{ "username": loginDto.Username, "success": bool }`.
        - By-id actions (`IdArgument` set) → `{ "entityId": routeArg, "success": bool }`.
        - POST creates (no `IdArgument`) → read `IAuditableResult.EntityId` from `ObjectResult.Value`; `{ "entityId": id_or_null, "success": bool }`.
        - `CDS_EVALUATE` → `{ "patientId": requestDto.PatientId_or_null, "success": bool }`.
      - Serialize via `System.Text.Json.JsonSerializer.Serialize`.
      - `await auditService.LogAsync(userId, effectiveAction, detailsJson)`.
      File: `Middleware/AuditActionFilter.cs`

      Verify steps 39–41: `dotnet build` — 0 errors.

---

## Layer 12 — Controllers

All controllers: `[ApiController]`, explicit `[Route("api/...")]` (not `[Route("api/[controller]")]`), `ActionResult<T>` or `IActionResult` returns, constructor DI only.

- [ ] 42. **`Controllers/AuthController.cs`** — `[Route("api/auth")]`.
      - `POST register`: `[Authorize(Roles="Administrator")]`, `[AuditAction("REGISTER")]`. Returns `Ok(UserDto)`.
      - `POST login`: `[AllowAnonymous]`, `[AuditAction("LOGIN")]`. Returns `Ok(LoginResponseDto)`.

- [ ] 43. **`Controllers/MedicalRecordsController.cs`** — `[Route("api/medical-records")]`. (**NEW**)
      - `POST ""`: `[Authorize(Roles="Doctor,Nurse")]`, `[AuditAction("CREATE_MEDICAL_RECORD")]`. Returns `Created("/api/medical-records/{id}", dto)`.

- [ ] 44. **`Controllers/PatientsController.cs`** — `[Route("api/patients")]`.
      - `GET ""`: `[Authorize(Roles="Doctor,Nurse,LaboratoryStaff")]`. `[FromQuery] int page=1, int pageSize=20, string? search`.
      - `POST ""`: `[Authorize(Roles="Doctor,Nurse")]`, `[AuditAction("CREATE_PATIENT")]`. Returns `Created`.
      - `GET {id:int}`: `[Authorize(Roles="Doctor,Nurse,LaboratoryStaff")]`, `[AuditAction("VIEW_PATIENT", "id")]`.
      - `PUT {id:int}`: `[Authorize(Roles="Doctor,Nurse")]`, `[AuditAction("UPDATE_PATIENT", "id")]`.

- [ ] 45. **`Controllers/CdsController.cs`** — `[Route("api/cds")]`.
      - `POST evaluate`: `[Authorize(Roles="Doctor,Nurse")]`, `[AuditAction("CDS_EVALUATE")]`.

- [ ] 46. **`Controllers/LaboratoryController.cs`** — `[Route("api/laboratory")]`.
      - `POST order`: `[Authorize(Roles="Doctor,Nurse")]`, `[AuditAction("CREATE_LAB_ORDER")]`. Reads `orderedByUserId` from `ICurrentUserService.GetUserId()`.
      - `PUT results/{id:int}`: `[Authorize(Roles="LaboratoryStaff")]`, `[AuditAction("UPDATE_LAB_RESULT", "id")]`.
      - `GET patient/{patientId:int}`: `[Authorize(Roles="Doctor,Nurse,LaboratoryStaff")]`, `[AuditAction("VIEW_LAB_HISTORY", "patientId")]`.

- [ ] 47. **`Controllers/MedicationsController.cs`** — `[Route("api/medications")]`.
      - `POST ""`: `[Authorize(Roles="Doctor")]`, `[AuditAction("CREATE_MEDICATION")]`. Reads `currentUserId` from `ICurrentUserService`. Returns `Created($"/api/medications/{result.Medication.MedicationId}", result)`.
      - `PUT {id:int}/discontinue`: `[Authorize(Roles="Doctor")]`, `[AuditAction("DISCONTINUE_MEDICATION", "id")]`.

- [ ] 48. **`Controllers/AppointmentsController.cs`** — `[Route("api/appointments")]`.
      - `POST ""`: `[Authorize(Roles="Doctor,Nurse,Administrator")]`, `[AuditAction("CREATE_APPOINTMENT")]`.
      - `GET doctor/{doctorId:int}`: `[Authorize(Roles="Doctor,Nurse,Administrator")]` (no audit).

- [ ] 49. **`Controllers/ReportsController.cs`** — `[Route("api/reports")]`.
      - `GET monthly-stats`: `[Authorize(Roles="Administrator")]`. Defaults: `month ??= DateTime.UtcNow.Month`, `year ??= DateTime.UtcNow.Year`. Range validation is in the service.

- [ ] 50. **`Controllers/AuditController.cs`** — `[Route("api/audit")]`.
      - `GET ""`: `[Authorize(Roles="Administrator")]`. Clamp page/pageSize in controller (not service). Return `PagedResult<AuditLogDto>`.

      Verify steps 42–50: `dotnet build` — 0 errors.

---

## Layer 13 — Program.cs + appsettings

- [ ] 51. **Rewrite `DCDSS/Program.cs`** with the full DI + pipeline per design §16.

      Registration order (exact — order matters for middleware and validator):
      1. `AddControllers(o => o.Filters.Add<AuditActionFilter>())`.
      2. `AddEndpointsApiExplorer()`.
      3. `AddSwaggerGen(...)` with Bearer security definition + requirement.
      4. `AddDbContextFactory<DcdssContext>(o => o.UseMySql(conn, new MySqlServerVersion(new Version(8, 0, 34))))`.
      5. `Configure<JwtSettings>(...)`, `Configure<CorsSettings>(...)`, `Configure<AdminSeedSettings>(...)`, `AddSingleton<IValidateOptions<JwtSettings>, JwtSettingsValidator>()`, `AddOptions<JwtSettings>().Bind(...).ValidateOnStart()`.
      6. `AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(...)` with explicit `NameClaimType="name"`, `RoleClaimType="role"`, `ClockSkew=TimeSpan.FromMinutes(1)`.
      7. `AddAuthorization(o => o.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())`.
      8. `AddCors(...)` using `cors.AllowedOrigins.Split(',', ...)`.
      9. `AddHealthChecks().AddDbContextCheck<DcdssContext>()`.
      10. `AddHttpContextAccessor()`.
      11. All scoped repos: 8 specific + `IUnitOfWork` + `IRepository<>` (open generic).
      12. All scoped services (11 total).
      13. Singletons: `IClinicalRuleStore`, `IReferenceRangeParser`, `ITokenService`.
      14. `AddScoped<ICurrentUserService, CurrentUserService>()`.
      15. `AddTransient<PasswordHasher<User>>()`.

      Pipeline order:
      1. `app.UseMiddleware<GlobalExceptionMiddleware>()` ← **FIRST**.
      2. `if (!IsDevelopment) app.UseHsts()`.
      3. `app.UseHttpsRedirection()`.
      4. `app.UseCors("Default")`.
      5. `app.UseAuthentication()` ← must be before `UseAuthorization`.
      6. `app.UseAuthorization()`.
      7. `app.MapHealthChecks("/health")`.
      8. `app.MapControllers()`.
      9. `if (IsDevelopment) { app.UseSwagger(); app.UseSwaggerUI(); }`.
      10. Admin seeder scope → `await seeder.SeedAsync()`.
      11. `app.Run()`.

      Files: `DCDSS/Program.cs`

- [ ] 52. **`DCDSS/appsettings.json`** — overwrite with exact JSON from design §11. Key point: `SigningKey = "REPLACE_ME"` → app will not start in production without override.

- [ ] 53. **`DCDSS/appsettings.Development.json`** — overwrite with dev overrides from design §11. Key point: `SigningKey = "DevOnlySigningKey_32bytes_minimum_DO_NOT_USE_IN_PROD"`, `AdminSeed.Password = "Admin@12345"`.

      Verify steps 51–53: `dotnet build "c:\Users\nm\IdeaProjects\Digital Clinical Decision Support System (DCDSS)\DCDSS.sln"` — 0 errors, 0 warnings.

---

## Layer 14 — EF Core Migration

- [ ] 54. **Create `InitialCreate` migration**.

      Preferred path:
      ```powershell
      dotnet tool install --global dotnet-ef --version 8.0.33
      $env:ASPNETCORE_ENVIRONMENT = "Development"
      cd "c:\Users\nm\IdeaProjects\Digital Clinical Decision Support System (DCDSS)\DCDSS"
      dotnet ef migrations add InitialCreate
      ```
      The `ASPNETCORE_ENVIRONMENT=Development` is required so that `appsettings.Development.json` overrides the `REPLACE_ME` signing key (otherwise `JwtSettingsValidator` fails the startup check, blocking the design-time factory).

      Fallback (if `dotnet-ef` unavailable): hand-author `Migrations/<timestamp>_InitialCreate.cs`, `..._InitialCreate.Designer.cs`, and `Migrations/DcdssContextModelSnapshot.cs` matching the `DcdssContext` + all 8 configurations.

      Note: `dotnet ef database update` requires a live MySQL instance — do NOT run it in an environment without MySQL.

      Files: `DCDSS/Migrations/<timestamp>_InitialCreate.cs`, `..._InitialCreate.Designer.cs`, `Migrations/DcdssContextModelSnapshot.cs`
      Verify: migration files exist in `Migrations/`; `dotnet build` still passes.

---

## Layer 15 — README

- [ ] 55. **`README.md`** at repo root (`c:\Users\nm\IdeaProjects\Digital Clinical Decision Support System (DCDSS)\README.md`).

      Required sections per design §14:
      1. Title & Overview
      2. Prerequisites (.NET 8 SDK, MySQL 8, dotnet-ef install command)
      3. Restore & Build
      4. Configure MySQL
      5. EF Core Migrations
      6. Initial Administrator (dev password, production random-generate flow, rotation note)
      7. Run the API (Swagger URL, /health)
      8. Authentication (JWT flow, production key override requirement)
      9. Endpoint Reference (table of all 17 endpoints)
      10. Project Structure (folder map)
      11. Assumptions & Known Limitations (UTC/SAST, no DELETE, Indeterminate status, normalized allergies, canonical drug names, no forced password change)

      File: `README.md`
      Verify: file exists; `dotnet build` unaffected.

---

## Final Verification Checklist

After all steps complete, run this checklist:

### Build
```powershell
dotnet build "c:\Users\nm\IdeaProjects\Digital Clinical Decision Support System (DCDSS)\DCDSS.sln"
```
Expected: `Build succeeded. 0 Error(s) 0 Warning(s)`

### DI checklist (review Program.cs)
- [ ] `AddDbContextFactory<DcdssContext>` present, no `AddDbContext`.
- [ ] `AddAuthentication` before `AddAuthorization`.
- [ ] `AddOptions<JwtSettings>().ValidateOnStart()` present.
- [ ] `IValidateOptions<JwtSettings>` registered as singleton.
- [ ] `GlobalExceptionMiddleware` is the first `Use*` call after `app.Build()`.
- [ ] `UseAuthentication()` before `UseAuthorization()`.
- [ ] `FallbackPolicy = RequireAuthenticatedUser` in `AddAuthorization`.
- [ ] `IAdminSeeder.SeedAsync()` called in a scope after `Build()`, before `Run()`.
- [ ] `IRepository<>` registered as open generic scoped.
- [ ] All 8 specific repos registered as scoped.
- [ ] `IClinicalRuleStore`, `IReferenceRangeParser`, `ITokenService` registered as singleton.
- [ ] `AuditActionFilter` registered via `MvcOptions.Filters.Add<AuditActionFilter>()`.

### Controller route/verb checklist
| Route | Verb | Roles |
|---|---|---|
| `api/auth/register` | POST | Administrator |
| `api/auth/login` | POST | AllowAnonymous |
| `api/medical-records` | POST | Doctor, Nurse |
| `api/patients` | GET | Doctor, Nurse, LaboratoryStaff |
| `api/patients` | POST | Doctor, Nurse |
| `api/patients/{id}` | GET | Doctor, Nurse, LaboratoryStaff |
| `api/patients/{id}` | PUT | Doctor, Nurse |
| `api/cds/evaluate` | POST | Doctor, Nurse |
| `api/laboratory/order` | POST | Doctor, Nurse |
| `api/laboratory/results/{id}` | PUT | LaboratoryStaff |
| `api/laboratory/patient/{patientId}` | GET | Doctor, Nurse, LaboratoryStaff |
| `api/medications` | POST | Doctor |
| `api/medications/{id}/discontinue` | PUT | Doctor |
| `api/appointments` | POST | Doctor, Nurse, Administrator |
| `api/appointments/doctor/{doctorId}` | GET | Doctor, Nurse, Administrator |
| `api/reports/monthly-stats` | GET | Administrator |
| `api/audit` | GET | Administrator |

### Audit action checklist
| Action name | Trigger |
|---|---|
| `REGISTER` | POST /api/auth/register |
| `LOGIN` | POST /api/auth/login (success) |
| `LOGIN_FAILED` | POST /api/auth/login (failure — filter switches name) |
| `CREATE_MEDICAL_RECORD` | POST /api/medical-records |
| `CREATE_PATIENT` | POST /api/patients |
| `UPDATE_PATIENT` | PUT /api/patients/{id} |
| `VIEW_PATIENT` | GET /api/patients/{id} |
| `CDS_EVALUATE` | POST /api/cds/evaluate |
| `CREATE_LAB_ORDER` | POST /api/laboratory/order |
| `UPDATE_LAB_RESULT` | PUT /api/laboratory/results/{id} |
| `VIEW_LAB_HISTORY` | GET /api/laboratory/patient/{patientId} |
| `CREATE_MEDICATION` | POST /api/medications |
| `DISCONTINUE_MEDICATION` | PUT /api/medications/{id}/discontinue |
| `CREATE_APPOINTMENT` | POST /api/appointments |

### Cross-cutting gotchas (coder must not miss)
- `Patient.DateOfBirth` is `DateOnly`, not `DateTime`. Column name `DOB`. Pomelo 8.x maps to MySQL `DATE`.
- `PatientAllergy` is the **8th** entity — ensure it has a `DbSet` in `DcdssContext` and an `IEntityTypeConfiguration`.
- `AuditLog.UserId` is **nullable int** (`int?`) — do not add a non-null constraint in the config.
- `PasswordHash` is `MaxLength(512)` — not 256.
- `GlobalExceptionMiddleware` must be registered **first** — `app.UseMiddleware<GlobalExceptionMiddleware>()` is line 1 after `app.Build()`.
- `[AllowAnonymous]` on the `login` action only — **no other action** should be anonymous.
- `AuditActionFilter` must **not** set `ExceptionHandled` — it must let `GlobalExceptionMiddleware` convert exceptions to HTTP responses.
- `AuditLogService.LogAsync` swallows all exceptions — catch all and log at Error, never rethrow.
- `AdminSeeder`: if `Password` is empty/null/whitespace → generate random password and log at **Warning** (not Info — so it is visible).
- `ISafetyCheckService` and `ICdsRuleEvaluator` both use `LabResultSelector.LatestPerTest` for "most recent per test" — use the same static helper, not duplicated logic.
- `ReferenceRangeParser.Classify`: threshold-form values (`<5`, `>200`) → `Indeterminate`. **Do not** extract the bound and classify it as a numeric result value.
- `ClinicalRuleStore`: ALG-002 is split into `ALG-002a` (allergen `"Sulfa"`) and `ALG-002b` (allergen `"Sulfonamide"`). Each has a single `Allergen` string.
- `MedicationService.PrescribeAsync`: `StartDate` validation (not in future) is **service-enforced** `BusinessRuleException` — no data annotation.
- `AppointmentService.CreateAsync`: `AppointmentDate > DateTime.UtcNow` — **strictly future** (equal to now → reject).
- `ReportService`: month 1–12, year ≥ 2000 — `BusinessRuleException` (not pagination clamping).
- `PatientService`: allergy set replace = `DeleteByPatientAsync` then `AddAsync` for each row. Do not partially update.
- `PrescribeMedicationResponseDto` must implement `IAuditableResult` (the audit filter reads `EntityId` from the `ObjectResult.Value`).
- `appsettings.json` ships `SigningKey = "REPLACE_ME"` — this is intentional; the validator rejects it on startup so operators are forced to override.
