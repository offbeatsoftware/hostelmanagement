# Database Schema

File: `C:\HostelData\Database\HostelManagement.accdb` (Microsoft Access 2007+ format).

The database and all tables are created automatically on first start (`Data/DatabaseInitializer.cs`).
On later starts only missing tables are created; existing tables and data are never changed.
The DDL is in `Data/DatabaseSchema.cs`.

## Design principles

- **No stored calculations.** Values that can be calculated are calculated, so they can never be out of date:
  - Room occupancy and available capacity come from current `RoomAllocation` rows.
  - Invoice paid amount, pending amount and status (Unpaid / Partly paid / Paid) come from `Payment` rows.
- **History is kept.** Check-out and room transfer close an allocation (set `CheckOutDate` and `Status`)
  instead of deleting it.
- **Files outside the database.** Photos and documents are stored in `C:\HostelData\Photos` and
  `C:\HostelData\Documents`; only the file path is stored.
- **Money** uses the Access `Currency` type (exact, no rounding errors).

## Tables

### Hostel
One row with hostel and college details.

| Column | Type | Notes |
|---|---|---|
| HostelId | AutoNumber | Primary key |
| HostelName | Text(150) | Required |
| Address | Text(255) | |
| Phone | Text(20) | |
| Email | Text(150) | |
| CollegeName | Text(150) | |
| CollegeAddress | Text(255) | |
| CreatedDate | Date/Time | Required |
| UpdatedDate | Date/Time | |

### Room
| Column | Type | Notes |
|---|---|---|
| RoomId | AutoNumber | Primary key |
| RoomNumber | Text(20) | Required, **unique** |
| Floor | Text(20) | Text so values like "Ground" are allowed |
| Capacity | Number | Required |
| SharingType | Text(20) | Required: Single / Double / Triple |
| Rent | Currency | Required |
| IsActive | Yes/No | Active / inactive |
| Remarks | Text(255) | |

### Student
| Column | Type | Notes |
|---|---|---|
| StudentId | AutoNumber | Primary key |
| StudentName | Text(150) | Required, indexed for search |
| DateOfBirth | Date/Time | |
| Gender | Text(20) | |
| Address | Text(255) | |
| CollegeName | Text(150) | |
| Course | Text(100) | |
| ClassName | Text(50) | The specification's "Class" (renamed: `Class` is a risky name in Access SQL) |
| Mobile | Text(20) | Indexed for search |
| Email | Text(150) | |
| PhotoPath | Text(255) | Path relative to the data folder |
| AadhaarLast4 | Text(4) | **Last 4 digits only** (see open questions) |
| AdmissionDate | Date/Time | |
| Status | Text(20) | Required: e.g. Active / Left |
| Remarks | Text(255) | |

### Parent
Parents/guardians; a student can have more than one.

| Column | Type | Notes |
|---|---|---|
| ParentId | AutoNumber | Primary key |
| StudentId | Number | Required, → Student |
| ParentName | Text(150) | Required |
| Relationship | Text(50) | |
| Mobile | Text(20) | |
| Email | Text(150) | |
| Address | Text(255) | |
| IsPrimaryContact | Yes/No | Which parent receives invoice and reminder emails |

### RoomAllocation
| Column | Type | Notes |
|---|---|---|
| AllocationId | AutoNumber | Primary key |
| StudentId | Number | Required, → Student |
| RoomId | Number | Required, → Room |
| CheckInDate | Date/Time | Required |
| CheckOutDate | Date/Time | Empty while the student is in the room |
| Status | Text(20) | Required: Current / Transferred / CheckedOut (indexed) |
| Remarks | Text(255) | |

### Service
| Column | Type | Notes |
|---|---|---|
| ServiceId | AutoNumber | Primary key |
| ServiceName | Text(100) | Required, **unique** |
| Rate | Currency | Required |
| IsActive | Yes/No | |

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
| Room: `Status` became `IsActive` | The only stated statuses are active/inactive; occupancy is calculated. |
| Student: `Class` became `ClassName` | Avoids an Access reserved word problem. |
| Student: `AadhaarReference` became `AadhaarLast4` | Store only what is needed (see open questions). |
| Parent: added `IsPrimaryContact` | Invoice and reminder emails need one recipient when a student has several parents. |
| EmailHistory: added `InvoiceId` | Shows which invoice an email was about. |

## Open questions

1. **Aadhaar:** is storing the last 4 digits enough for identification? Storing full Aadhaar numbers is not
   recommended for a private hostel. If the full number is needed, the column has to be widened before go-live.
2. **Room capacity vs sharing type:** is capacity always the same as the sharing type
   (Single = 1, Double = 2, Triple = 3)? If so, Phase 4 will set capacity automatically.
3. **Payments without an invoice (advance payments):** `Payment.InvoiceId` allows empty values so this is
   possible later, but until confirmed the application will require an invoice for every payment.
4. **Student documents:** the specification mentions a Documents folder, but no document screen or table.
   If documents (ID copies, admission forms) are needed, a small `StudentDocument` table will be added in Phase 5.

## Changing the schema before go-live

While there is no real data, the simplest way to apply a schema change is to close the application, delete
`C:\HostelData\Database\HostelManagement.accdb` and start the application again. After go-live,
schema changes will be applied with explicit upgrade steps instead.
