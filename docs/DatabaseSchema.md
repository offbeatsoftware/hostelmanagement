# Database Schema

File: `Database\HostelManagement.accdb` next to `HostelManagement.exe` (Microsoft Access 2007+ format).
The empty database is kept in the repository at `HostelManagement/Database/HostelManagement.accdb`.

The database and all tables are created automatically on first start (`Data/DatabaseInitializer.cs`).
On later starts only missing tables are created; existing tables and data are never changed.
The DDL is in `Data/DatabaseSchema.cs`.

## Several hostels

The application manages several hostels. The admin selects one hostel at a time (on the sign in screen and
at the top right of the main window); colleges, rooms, students and billing screens show only that hostel.

```
Hostel 1──* College 1──* Student
Hostel 1──* SharingType 1──* Room
Hostel 1──* Room
```

- A college belongs to one hostel; a hostel has students from several colleges. If the same college sends
  students to two hostels, it is added under each hostel.
- A student belongs to a college and through it to the college's hostel (no separate hostel column, so the
  two can never disagree).
- Rooms and their rent (per sharing type) belong to a hostel. Room numbers and college names only need to be
  unique within a hostel.

## Design principles

- **No stored calculations.** Values that can be calculated are calculated, so they can never be out of date:
  - Room occupancy and available capacity come from current `RoomAllocation` rows.
  - Invoice paid amount, pending amount and status (Unpaid / Partly paid / Paid) come from `Payment` rows.
- **History is kept.** Check-out and room transfer close an allocation (set `CheckOutDate` and `Status`)
  instead of deleting it.
- **Files outside the database.** Photos and documents are stored in the `Photos` and `Documents`
  folders next to the application; only the file path is stored.
- **Money** uses the Access `Currency` type (exact, no rounding errors).

## Schema version

The `SchemaInfo` table holds one row with the schema version (`DatabaseSchema.Version`, currently **7**).
At startup the application refuses a database with an older or newer version and explains what to do,
instead of failing later with confusing errors. Increase the version whenever a table or column changes.

| Version | Change |
|---|---|
| 1 | Initial schema (Phase 2), no SchemaInfo table |
| 2 | Phase 3: College table; Student.CollegeName replaced by Student.CollegeId; college columns removed from Hostel |
| 3 | Phase 4: SharingType table (capacity and rent per sharing type); Room keeps only SharingTypeId |
| 4 | Several hostels: HostelId on College, SharingType and Room; Student.CollegeId required |
| 7 | Phase 7: Hostel.BillingFrequency; Service per hostel (included in rent or extra per month); StudentService |
| 6 | Phase 6: Room.Gender (rooms are for boys or girls) |
| 5 | Phase 5: full Aadhaar number and Aadhaar card file on Student; student mobile and admission date required; parent mobile and email required |

## Tables

### SchemaInfo
| Column | Type | Notes |
|---|---|---|
| Version | Number | Required; one row |

### Hostel
Any number of hostels.

| Column | Type | Notes |
|---|---|---|
| HostelId | AutoNumber | Primary key |
| HostelName | Text(150) | Required, **unique** (also checked ignoring upper/lower case) |
| Address | Text(255) | |
| Phone | Text(20) | |
| Email | Text(150) | |
| BillingFrequency | Text(20) | Required: HalfYearly (twice a year) / Quarterly (4 times a year) |
| CreatedDate | Date/Time | Required |
| UpdatedDate | Date/Time | |

A hostel can only be deleted when it has no colleges and no rooms.

**Billing (client decisions, Phase 7):** rent is entered per **year** and billed in equal installments, twice
or four times a year (chosen per hostel). Billing periods follow the academic year starting in **July**:
Jul to Dec and Jan to Jun, or Jul to Sep, Oct to Dec, Jan to Mar and Apr to Jun. Extra services are charged
per **month**.

### College
Colleges whose students stay in a hostel.

| Column | Type | Notes |
|---|---|---|
| CollegeId | AutoNumber | Primary key |
| HostelId | Number | Required, → Hostel |
| CollegeName | Text(150) | Required, **unique within the hostel** (also ignoring upper/lower case) |
| Address | Text(255) | |
| Phone | Text(20) | |

A college cannot be deleted while students are linked to it.

### SharingType
Single, Double and Triple sharing, added automatically for every new hostel with rent 0.
Capacity always equals the sharing type and rent is per sharing type and hostel (client decisions).

| Column | Type | Notes |
|---|---|---|
| SharingTypeId | AutoNumber | Primary key |
| HostelId | Number | Required, → Hostel |
| SharingName | Text(20) | Required, **unique within the hostel**: Single / Double / Triple |
| Capacity | Number | Required: 1 / 2 / 3 |
| Rent | Currency | Required; **yearly** rent per student, set by the admin on the Rooms screen |

### Room
| Column | Type | Notes |
|---|---|---|
| RoomId | AutoNumber | Primary key |
| HostelId | Number | Required, → Hostel |
| RoomNumber | Text(20) | Required, **unique within the hostel** (also ignoring upper/lower case) |
| Floor | Text(20) | Text so values like "Ground" are allowed |
| SharingTypeId | Number | Required, → SharingType of the same hostel (gives the room's capacity and rent) |
| Gender | Text(10) | Required: Male / Female, shown as Boys / Girls; only students of that gender can be allocated |
| IsActive | Yes/No | Active / inactive |
| Remarks | Text(255) | |

Room rules: a room cannot be changed to a sharing type smaller than its current occupancy, cannot be made
inactive while students are in it, and cannot be deleted once any student has stayed in it (mark it inactive
instead). Students can only be allocated to an active room with a free bed.

### Student
| Column | Type | Notes |
|---|---|---|
| StudentId | AutoNumber | Primary key |
| StudentName | Text(150) | Required, indexed for search |
| DateOfBirth | Date/Time | Must be in the past |
| Gender | Text(20) | Male / Female / Other |
| Address | Text(255) | |
| CollegeId | Number | Required, → College (the student's hostel is the college's hostel; cannot move to another hostel's college) |
| Course | Text(100) | |
| ClassName | Text(50) | The specification's "Class" (renamed: `Class` is a risky name in Access SQL) |
| Mobile | Text(20) | Required, indexed for search |
| Email | Text(150) | |
| PhotoPath | Text(255) | Relative to the application folder, e.g. `Photos\Students\S12_photo_20261005103000123.jpg` |
| AadhaarNumber | Text(12) | Full 12 digit number (client decision), checked with the Verhoeff check digit, unique; shown masked (`XXXX XXXX 1234`) in lists |
| AadhaarCardPath | Text(255) | Scanned Aadhaar card (PDF/JPG/PNG), e.g. `Documents\Students\S12_aadhaar_....pdf` |
| AdmissionDate | Date/Time | Required; after the date of birth |
| Status | Text(20) | Required: Active / Left |
| Remarks | Text(255) | |

Files are copied into the application's folders under generated names (never the original file name),
max 5 MB; photos must be readable images and PDFs must be real PDFs. A student with room, invoice, payment or
email history cannot be deleted (set the status to Left instead).

### Parent
Parents/guardians; a student has at least one, and exactly one primary contact.

| Column | Type | Notes |
|---|---|---|
| ParentId | AutoNumber | Primary key |
| StudentId | Number | Required, → Student |
| ParentName | Text(150) | Required |
| Relationship | Text(50) | |
| Mobile | Text(20) | Required |
| Email | Text(150) | Required |
| Address | Text(255) | |
| IsPrimaryContact | Yes/No | Exactly one per student: receives invoice and reminder emails |

The primary parent is entered together with the student; more guardians are added on the Parents / Guardians
screen. A student's only parent cannot be deleted; deleting the primary contact makes another parent primary.

### RoomAllocation
A student's stay in a room. The history is never deleted.

| Column | Type | Notes |
|---|---|---|
| AllocationId | AutoNumber | Primary key |
| StudentId | Number | Required, → Student |
| RoomId | Number | Required, → Room |
| CheckInDate | Date/Time | Required; not before the admission date, not in the future |
| CheckOutDate | Date/Time | Empty while the student is in the room |
| Status | Text(20) | Required: Current / Transferred / CheckedOut (indexed) |
| Remarks | Text(255) | Check-in remarks, with transfer/check-out remarks added |

Allocation rules (client decisions, Phase 6):
- A student is in at most one room at a time (one Current allocation).
- The room must be in the student's hostel, active, for the student's gender, with a free bed.
- Only Active students with a gender (Male/Female) can be checked in.
- **Transfer**: the current allocation ends (Transferred, CheckOutDate = date) and a new Current allocation
  starts on the same date.
- **Check-out**: the allocation ends (CheckedOut) and the student's status is set to Left, in one transaction.
- A student in a room cannot be set to Left or change gender on the Students screen.

### Service
Services of a hostel. Every new hostel gets Wi-Fi and Laundry (included in the rent) and Transport
(extra, monthly rate to be set).

| Column | Type | Notes |
|---|---|---|
| ServiceId | AutoNumber | Primary key |
| HostelId | Number | Required, → Hostel |
| ServiceName | Text(100) | Required, **unique within the hostel** |
| IsIncludedInRent | Yes/No | Included in the rent (no charge) or charged extra |
| MonthlyRate | Currency | Required; monthly charge per student for extra services, 0 when included |
| IsActive | Yes/No | |

A service in use cannot be made inactive or included in rent; a service any student has used cannot be deleted.

### StudentService
Extra services (such as transport) a student uses, for billing.

| Column | Type | Notes |
|---|---|---|
| StudentServiceId | AutoNumber | Primary key |
| StudentId | Number | Required, → Student |
| ServiceId | Number | Required, → Service (an active extra service of the student's hostel) |
| StartDate | Date/Time | Required; the admission date for a new student, otherwise the day it was ticked |
| EndDate | Date/Time | Empty while in use; the day it was unticked |

### Invoice
| Column | Type | Notes |
|---|---|---|
| InvoiceId | AutoNumber | Primary key |
| InvoiceNumber | Text(30) | Required, **unique** |
| StudentId | Number | Required, → Student |
| InvoiceDate | Date/Time | Required, indexed |
| BillingFrom | Date/Time | Required |
| BillingTo | Date/Time | Required |
| TotalAmount | Currency | Required; equals the sum of the invoice items, saved together with them |

### InvoiceItem
Invoice lines (rent and each service).

| Column | Type | Notes |
|---|---|---|
| InvoiceItemId | AutoNumber | Primary key |
| InvoiceId | Number | Required, → Invoice |
| Description | Text(150) | Required |
| Quantity | Number | Required |
| Rate | Currency | Required |
| Amount | Currency | Required (Quantity × Rate at the time of invoicing) |

### Payment
| Column | Type | Notes |
|---|---|---|
| PaymentId | AutoNumber | Primary key |
| StudentId | Number | Required, → Student |
| InvoiceId | Number | → Invoice (see open questions) |
| PaymentDate | Date/Time | Required, indexed |
| Amount | Currency | Required |
| PaymentMethod | Text(30) | Required |
| Reference | Text(100) | |
| Remarks | Text(255) | |

### EmailHistory
| Column | Type | Notes |
|---|---|---|
| EmailHistoryId | AutoNumber | Primary key |
| StudentId | Number | Required, → Student |
| InvoiceId | Number | → Invoice (the invoice that was emailed or reminded) |
| RecipientEmail | Text(150) | Required |
| EmailType | Text(30) | Required: Invoice / DueReminder |
| Subject | Text(255) | |
| SentDate | Date/Time | Required |
| Status | Text(20) | Required: Sent / Failed |
| ErrorMessage | Text(255) | |

## Changes from the suggested design in the specification

| Change | Reason |
|---|---|
| Invoice: removed `RentAmount`, `ServiceAmount` | Already held as invoice items. |
| Invoice: removed `PaidAmount`, `PendingAmount`, `Status` | Calculated from payments (Pending = Total − Payments), so they can never disagree. |
| RoomAllocation: removed `SharingType` | Already held on the room. |
| Several hostels: `HostelId` on College, SharingType and Room | Client decision: multiple hostels, each with its own colleges, rooms and rent. |
| Hostel: removed `CollegeName`, `CollegeAddress`; added College table | Students come from several colleges (client decision, Phase 3). |
| Student: `CollegeName` became `CollegeId` | Each student is linked to a college from the list. |
| Added `SchemaInfo` | Detects databases with an outdated layout. |
| Room: `Capacity`, `SharingType`, `Rent` replaced by `SharingTypeId`; added SharingType table | Capacity equals the sharing type and rent is per sharing type (client decisions, Phase 4). |
| Room: `Status` became `IsActive` | The only stated statuses are active/inactive; occupancy is calculated. |
| Student: `Class` became `ClassName` | Avoids an Access reserved word problem. |
| Student: `AadhaarReference` became `AadhaarNumber` + `AadhaarCardPath` | Client decision: store the full number and a scan of the card. |
| Parent: added `IsPrimaryContact` | Invoice and reminder emails need one recipient when a student has several parents. |
| EmailHistory: added `InvoiceId` | Shows which invoice an email was about. |

## Open questions

1. **Payments without an invoice (advance payments):** `Payment.InvoiceId` allows empty values so this is
   possible later, but until confirmed the application will require an invoice for every payment.

## Changing the schema

1. Change `Data/DatabaseSchema.cs` and increase `DatabaseSchema.Version`.
2. Push: the **Create database template** workflow rebuilds `HostelManagement/Database/HostelManagement.accdb`
   on Windows and commits it.
3. While there is no real data, existing databases are replaced: close the application, delete
   `Database\HostelManagement.accdb` next to the application and build/start it again (it says so itself).
   After go-live, schema changes will be applied with upgrade steps that keep the data.
