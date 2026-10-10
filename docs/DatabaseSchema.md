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
- Rooms belong to a hostel; the rent is agreed per student (Invoice), not per room. Room numbers and college names only need to be
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

The `SchemaInfo` table holds one row with the schema version (`DatabaseSchema.Version`, currently **13**).
At startup the application refuses a database with an older or newer version and explains what to do,
instead of failing later with confusing errors. Increase the version whenever a table or column changes.

| Version | Change |
|---|---|
| 1 | Initial schema (Phase 2), no SchemaInfo table |
| 2 | Phase 3: College table; Student.CollegeName replaced by Student.CollegeId; college columns removed from Hostel |
| 3 | Phase 4: SharingType table (capacity and rent per sharing type); Room keeps only SharingTypeId |
| 4 | Several hostels: HostelId on College, SharingType and Room; Student.CollegeId required |
| 5 | Phase 5: full Aadhaar number and Aadhaar card file on Student; student mobile and admission date required; parent mobile and email required |
| 6 | Phase 6: Room.Gender (rooms are for boys or girls) |
| 7 | Phase 7: Hostel.BillingFrequency; Service per hostel (included in rent or extra per month); StudentService |
| 8 | Phase 9: Payment.ReceiptNumber (unique) and Payment.CreatedDate; Payment.InvoiceId required |
| 9 | Phase 11: AppSetting table (Gmail account and email texts) |
| 10 | AdminUser table (admin login with hashed password, email and phone) |
| 11 | RoomAllocation.BedNumber (bed in the room, for the residency agreement) |
| 12 | Attendance table (night attendance, absence emails to parents) |
| 13 | Version 1.2 (client feedback): fee per student and academic year on Invoice (RoomRent, TransportAmount, AcademicYear, one per student and year); Student father and mother columns; removed Hostel.BillingFrequency, SharingType.Rent and the Parent, Service, StudentService and InvoiceItem tables |

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
| CreatedDate | Date/Time | Required |
| UpdatedDate | Date/Time | |

A hostel can only be deleted when it has no colleges and no rooms.

**Fees (client decisions, version 1.2):** the room rent and transport are agreed with each student for the
academic year (July to June) and stored on the student's invoice for that year (see Invoice).

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
Single, Double and Triple sharing, added automatically for every new hostel. The sharing type only decides how
many students fit in a room: capacity equals the sharing type (client decisions). It has no rent.

| Column | Type | Notes |
|---|---|---|
| SharingTypeId | AutoNumber | Primary key |
| HostelId | Number | Required, → Hostel |
| SharingName | Text(20) | Required, **unique within the hostel**: Single / Double / Triple |
| Capacity | Number | Required: 1 / 2 / 3 |

### Room
| Column | Type | Notes |
|---|---|---|
| RoomId | AutoNumber | Primary key |
| HostelId | Number | Required, → Hostel |
| RoomNumber | Text(20) | Required, **unique within the hostel** (also ignoring upper/lower case) |
| Floor | Text(20) | Text so values like "Ground" are allowed |
| SharingTypeId | Number | Required, → SharingType of the same hostel (gives the room's capacity) |
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
| FatherName | Text(150) | Required |
| FatherMobile | Text(20) | Required |
| FatherEmail | Text(150) | Invoices, receipts and fee reminders go here (to MotherEmail when empty) |
| MotherName | Text(150) | |
| MotherMobile | Text(20) | |
| MotherEmail | Text(150) | Attendance emails go here (to FatherEmail when empty) |
| PhotoPath | Text(255) | Relative to the application folder, e.g. `Photos\Students\S12_photo_20261005103000123.jpg` |
| AadhaarNumber | Text(12) | Full 12 digit number (client decision), checked with the Verhoeff check digit, unique; shown masked (`XXXX XXXX 1234`) in lists |
| AadhaarCardPath | Text(255) | Scanned Aadhaar card (PDF/JPG/PNG), e.g. `Documents\Students\S12_aadhaar_....pdf` |
| AdmissionDate | Date/Time | Required; after the date of birth |
| Status | Text(20) | Required: Active / Left |
| Remarks | Text(255) | |

Files are copied into the application's folders under generated names (never the original file name),
max 5 MB; photos must be readable images and PDFs must be real PDFs. A student with room, invoice, payment or
email history cannot be deleted (set the status to Left instead).

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

`BedNumber` (Number): the bed in the room, 1 up to the room's capacity. At check-in and transfer the lowest
free bed is offered and can be changed; two current students can never share a bed, and a room cannot be made
smaller than a bed in use. The residency agreement (editable text, stored in `AppSetting` as `Agreement.Text`)
prints the room and bed.

### Invoice
A student's fee for one academic year.

| Column | Type | Notes |
|---|---|---|
| InvoiceId | AutoNumber | Primary key |
| InvoiceNumber | Text(30) | Required, **unique** |
| StudentId | Number | Required, → Student |
| InvoiceDate | Date/Time | Required, indexed (the check-in date, or the day the fee was entered) |
| AcademicYear | Number | Required: the year the academic year starts in (2026 = July 2026 to June 2027); **unique with StudentId** |
| RoomRent | Currency | Required, more than 0: the room rent agreed with the student for the year |
| TransportAmount | Currency | Required, 0 when the student does not use transport |
| Remarks | Text(255) | |

Rules (client decisions, version 1.2):
- The fee is agreed per student, not per room type: two students in the same Double room can pay different amounts.
- The invoice is created at check-in for the academic year of the check-in date, with the room rent and transport
  the admin enters. The full amount is due even when the student joins mid-year. Every new academic year the admin
  enters a new fee (New Year Fees). A transfer or check-out does not change the fee.
- Total = RoomRent + TransportAmount (calculated). The student pays any amount any number of times; paid and pending
  come from Payment. The admin can change the amounts (Edit Fee) but not below what is already paid.
- There are no due dates or overdue amounts: the admin sends fee reminders whenever he wants.
- Invoice number `SBH/2026-27/0001`: one running sequence per academic year, shared by all hostels so that every
  number is unique. No GST, deposits or discounts.
- An invoice can be deleted only while it has no payments and no emails.
- Invoice PDFs (with every payment, total paid and pending) are saved in the `Invoices` folder.

### Payment
| Column | Type | Notes |
|---|---|---|
| PaymentId | AutoNumber | Primary key |
| ReceiptNumber | Text(30) | Required, **unique**, for example `SBH/R/2026-27/0001` |
| StudentId | Number | Required, → Student (the invoice's student) |
| InvoiceId | Number | Required, → Invoice |
| PaymentDate | Date/Time | Required, indexed |
| Amount | Currency | Required |
| PaymentMethod | Text(30) | Required: Cash / UPI / Bank transfer / Cheque |
| Reference | Text(100) | Required for UPI, bank transfer and cheque |
| Remarks | Text(255) | |
| CreatedDate | Date/Time | Required; when the payment was entered |

Rules (Phase 9):
- Every payment is made against an invoice. Part payments are allowed; a payment can never be more than the
  amount still pending, so there are no advance or extra payments.
- The payment date cannot be in the future.
- Receipt number `SBH/R/2026-27/0001`: one running sequence per academic year of the payment date.
- A payment entered by mistake can be deleted; its amount becomes pending again on the invoice.
- Receipt PDFs (with the amount in words) are saved in the `Receipts` folder next to the application.

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

### Attendance
Night attendance, marked once a day by the admin.

| Column | Type | Notes |
|---|---|---|
| AttendanceId | AutoNumber | Primary key |
| StudentId | Number | Required, → Student |
| AttendanceDate | Date/Time | Required, indexed; **unique** together with StudentId (one record per student per night) |
| IsPresent | Yes/No | Required |
| Remarks | Text(255) | |
| MarkedDate | Date/Time | Required; when the attendance was last saved |
| ParentEmailedDate | Date/Time | When the parent was emailed about the absence (also logged in EmailHistory as `Absence`) |

Rules: the sheet of a date lists the students in a room of the hostel that night (checked in on or before the
date, not checked out or transferred that day); everyone starts as present. Future dates cannot be marked;
earlier dates can be opened and corrected. Parents of absent students are emailed only when the admin clicks
Email Parents, once per date unless the admin chooses to email again.

### AdminUser
The admin login. A new database gets the default login admin / admin, which the admin changes on the
Admin Account screen.

| Column | Type | Notes |
|---|---|---|
| AdminUserId | AutoNumber | Primary key |
| UserName | Text(50) | Required, **unique**; not case sensitive at sign in |
| PasswordHash | Text(255) | Required. Salted PBKDF2 SHA-256 hash (`pbkdf2-sha256$iterations$salt$hash`); the password itself is never stored |
| Email | Text(150) | Receives a copy (CC) of every email sent to parents |
| Phone | Text(20) | |
| CreatedDate | Date/Time | Required |
| UpdatedDate | Date/Time | |

### AppSetting
Application settings as key and value pairs (Phase 11).

| Column | Type | Notes |
|---|---|---|
| SettingKey | Text(50) | Primary key, for example `Email.SenderEmail`, `Email.Invoice.Subject` |
| SettingValue | Memo | The value. `Email.AppPassword` is encrypted with Windows (DPAPI) for the signed in Windows user, so after moving the database to another computer or Windows user it must be entered again |

Email rules: one Gmail account for all hostels; emails are sent only when the admin clicks a button, with a
copy to the admin. Invoices, receipts and fee reminders go to the father (the mother when the father has no
email), attendance emails to the mother (the father when the mother has no email). Fee reminders show the
total, paid and pending amounts and attach the invoice PDFs, for the students the admin ticks. Every email is
written to `EmailHistory` (`EmailType` Invoice / Receipt / DueReminder / Absence, `Status` Sent / Failed), one
row per invoice it was about.

## Changes from the suggested design in the specification

| Change | Reason |
|---|---|
| Invoice: `RentAmount` became `RoomRent`, `ServiceAmount` became `TransportAmount`; added `AcademicYear` | Version 1.2: one fee per student and academic year, agreed at check-in. |
| Invoice: removed `PaidAmount`, `PendingAmount`, `Status` | Calculated from payments (Pending = Total − Payments), so they can never disagree. |
| RoomAllocation: removed `SharingType` | Already held on the room. |
| Several hostels: `HostelId` on College, SharingType and Room | Client decision: multiple hostels, each with its own colleges and rooms. |
| Hostel: removed `CollegeName`, `CollegeAddress`; added College table | Students come from several colleges (client decision, Phase 3). |
| Student: `CollegeName` became `CollegeId` | Each student is linked to a college from the list. |
| Added `SchemaInfo` | Detects databases with an outdated layout. |
| Room: `Capacity`, `SharingType`, `Rent` replaced by `SharingTypeId`; added SharingType table | Capacity equals the sharing type; the rent is agreed per student (version 1.2). |
| Room: `Status` became `IsActive` | The only stated statuses are active/inactive; occupancy is calculated. |
| Student: `Class` became `ClassName` | Avoids an Access reserved word problem. |
| Student: `AadhaarReference` became `AadhaarNumber` + `AadhaarCardPath` | Client decision: store the full number and a scan of the card. |
| Parent table replaced by father and mother columns on Student | Version 1.2: the client records father and mother separately; fees go to the father, attendance to the mother. |
| EmailHistory: added `InvoiceId` | Shows which invoice an email was about. |

## Changing the schema

1. Change `Data/DatabaseSchema.cs` and increase `DatabaseSchema.Version`.
2. Push: the **Create database template** workflow rebuilds `HostelManagement/Database/HostelManagement.accdb`
   on Windows and commits it.
3. An existing database of an earlier version that has no hostel data yet is replaced automatically at start,
   keeping the admin login and settings (Gmail account, attendance email text, agreement text, backup folder).
   A database with data must be deleted by hand while there is no real data (the application says so).
   After go-live, schema changes will be applied with upgrade steps that keep the data.
