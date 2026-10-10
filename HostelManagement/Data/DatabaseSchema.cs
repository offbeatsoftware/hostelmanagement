namespace HostelManagement.Data;

/// <summary>A table and the DDL statements that create it (CREATE TABLE first, then indexes).</summary>
public sealed record TableDefinition(string Name, params string[] Statements);

/// <summary>
/// The Access database schema. Tables are listed so that a table is always created
/// after the tables it references. See docs/DatabaseSchema.md for the design notes.
///
/// Several hostels: colleges, sharing types and rooms belong to a hostel; a student
/// belongs to a college and through it to the college's hostel.
///
/// Fees are agreed per student (client decision, version 1.2): every academic year a student has one
/// invoice with the room rent and the transport amount for that year, decided by the admin.
///
/// Calculated values are not stored: room occupancy comes from RoomAllocation,
/// and invoice paid/pending amounts and status come from Payment.
/// </summary>
public static class DatabaseSchema
{
    /// <summary>
    /// Increase by one whenever a table or column changes, so databases with an older
    /// layout are detected at startup. Stored in the SchemaInfo table.
    /// </summary>
    public const int Version = 14;

    /// <summary>
    /// The oldest version that is upgraded in place, keeping its data (version 1.2 onwards). New tables are
    /// added by the normal start up; a later version that changes a column must add an upgrade step for it.
    /// </summary>
    public const int FirstUpgradableVersion = 13;

    /// <summary>
    /// The sharing types added to every new hostel. A sharing type only decides how many students
    /// fit in a room (client decision): capacity equals the sharing type, and it carries no rent.
    /// </summary>
    public static IReadOnlyList<(string Name, int Capacity)> DefaultSharingTypes { get; } =
    [
        ("Single", 1),
        ("Double", 2),
        ("Triple", 3),
    ];

    public static IReadOnlyList<TableDefinition> Tables { get; } =
    [
        new("SchemaInfo", """
            CREATE TABLE [SchemaInfo] (
                [Version] INTEGER NOT NULL
            )
            """),

        new("Hostel", """
            CREATE TABLE [Hostel] (
                [HostelId]       COUNTER CONSTRAINT [PK_Hostel] PRIMARY KEY,
                [HostelName]     TEXT(150) NOT NULL,
                [Address]        TEXT(255),
                [Phone]          TEXT(20),
                [Email]          TEXT(150),
                [CreatedDate]    DATETIME NOT NULL,
                [UpdatedDate]    DATETIME,
                CONSTRAINT [UQ_Hostel_HostelName] UNIQUE ([HostelName])
            )
            """),

        new("College", """
            CREATE TABLE [College] (
                [CollegeId]   COUNTER CONSTRAINT [PK_College] PRIMARY KEY,
                [HostelId]    INTEGER NOT NULL,
                [CollegeName] TEXT(150) NOT NULL,
                [Address]     TEXT(255),
                [Phone]       TEXT(20),
                CONSTRAINT [UQ_College_HostelName] UNIQUE ([HostelId], [CollegeName]),
                CONSTRAINT [FK_College_Hostel] FOREIGN KEY ([HostelId]) REFERENCES [Hostel] ([HostelId])
            )
            """),

        new("SharingType", """
            CREATE TABLE [SharingType] (
                [SharingTypeId] COUNTER CONSTRAINT [PK_SharingType] PRIMARY KEY,
                [HostelId]      INTEGER NOT NULL,
                [SharingName]   TEXT(20) NOT NULL,
                [Capacity]      INTEGER NOT NULL,
                CONSTRAINT [UQ_SharingType_HostelName] UNIQUE ([HostelId], [SharingName]),
                CONSTRAINT [FK_SharingType_Hostel] FOREIGN KEY ([HostelId]) REFERENCES [Hostel] ([HostelId])
            )
            """),

        new("Room", """
            CREATE TABLE [Room] (
                [RoomId]        COUNTER CONSTRAINT [PK_Room] PRIMARY KEY,
                [HostelId]      INTEGER NOT NULL,
                [RoomNumber]    TEXT(20) NOT NULL,
                [Floor]         TEXT(20),
                [SharingTypeId] INTEGER NOT NULL,
                [Gender]        TEXT(10) NOT NULL,
                [IsActive]      BIT NOT NULL,
                [Remarks]       TEXT(255),
                CONSTRAINT [UQ_Room_HostelNumber] UNIQUE ([HostelId], [RoomNumber]),
                CONSTRAINT [FK_Room_Hostel] FOREIGN KEY ([HostelId]) REFERENCES [Hostel] ([HostelId]),
                CONSTRAINT [FK_Room_SharingType] FOREIGN KEY ([SharingTypeId]) REFERENCES [SharingType] ([SharingTypeId])
            )
            """),

        new("Student", """
            CREATE TABLE [Student] (
                [StudentId]     COUNTER CONSTRAINT [PK_Student] PRIMARY KEY,
                [StudentName]   TEXT(150) NOT NULL,
                [DateOfBirth]   DATETIME,
                [Gender]        TEXT(20),
                [Address]       TEXT(255),
                [CollegeId]     INTEGER NOT NULL,
                [Course]        TEXT(100),
                [ClassName]     TEXT(50),
                [Mobile]        TEXT(20) NOT NULL,
                [Email]         TEXT(150),
                [FatherName]    TEXT(150) NOT NULL,
                [FatherMobile]  TEXT(20) NOT NULL,
                [FatherEmail]   TEXT(150),
                [MotherName]    TEXT(150),
                [MotherMobile]  TEXT(20),
                [MotherEmail]   TEXT(150),
                [PhotoPath]       TEXT(255),
                [AadhaarNumber]   TEXT(12),
                [AadhaarCardPath] TEXT(255),
                [AdmissionDate] DATETIME NOT NULL,
                [Status]        TEXT(20) NOT NULL,
                [Remarks]       TEXT(255),
                CONSTRAINT [FK_Student_College] FOREIGN KEY ([CollegeId]) REFERENCES [College] ([CollegeId])
            )
            """,
            "CREATE INDEX [IX_Student_StudentName] ON [Student] ([StudentName])",
            "CREATE INDEX [IX_Student_Mobile] ON [Student] ([Mobile])"),

        new("RoomAllocation", """
            CREATE TABLE [RoomAllocation] (
                [AllocationId] COUNTER CONSTRAINT [PK_RoomAllocation] PRIMARY KEY,
                [StudentId]    INTEGER NOT NULL,
                [RoomId]       INTEGER NOT NULL,
                [CheckInDate]  DATETIME NOT NULL,
                [CheckOutDate] DATETIME,
                [Status]       TEXT(20) NOT NULL,
                [Remarks]      TEXT(255),
                [BedNumber]    INTEGER,
                CONSTRAINT [FK_RoomAllocation_Student] FOREIGN KEY ([StudentId]) REFERENCES [Student] ([StudentId]),
                CONSTRAINT [FK_RoomAllocation_Room] FOREIGN KEY ([RoomId]) REFERENCES [Room] ([RoomId])
            )
            """,
            "CREATE INDEX [IX_RoomAllocation_Status] ON [RoomAllocation] ([Status])"),

        new("Invoice", """
            CREATE TABLE [Invoice] (
                [InvoiceId]     COUNTER CONSTRAINT [PK_Invoice] PRIMARY KEY,
                [InvoiceNumber] TEXT(30) NOT NULL,
                [StudentId]     INTEGER NOT NULL,
                [InvoiceDate]   DATETIME NOT NULL,
                [AcademicYear]  INTEGER NOT NULL,
                [RoomRent]      CURRENCY NOT NULL,
                [TransportAmount] CURRENCY NOT NULL,
                [Remarks]       TEXT(255),
                CONSTRAINT [UQ_Invoice_InvoiceNumber] UNIQUE ([InvoiceNumber]),
                CONSTRAINT [UQ_Invoice_StudentYear] UNIQUE ([StudentId], [AcademicYear]),
                CONSTRAINT [FK_Invoice_Student] FOREIGN KEY ([StudentId]) REFERENCES [Student] ([StudentId])
            )
            """,
            "CREATE INDEX [IX_Invoice_InvoiceDate] ON [Invoice] ([InvoiceDate])"),

        new("Payment", """
            CREATE TABLE [Payment] (
                [PaymentId]     COUNTER CONSTRAINT [PK_Payment] PRIMARY KEY,
                [ReceiptNumber] TEXT(30) NOT NULL,
                [StudentId]     INTEGER NOT NULL,
                [InvoiceId]     INTEGER NOT NULL,
                [PaymentDate]   DATETIME NOT NULL,
                [Amount]        CURRENCY NOT NULL,
                [PaymentMethod] TEXT(30) NOT NULL,
                [Reference]     TEXT(100),
                [Remarks]       TEXT(255),
                [CreatedDate]   DATETIME NOT NULL,
                CONSTRAINT [UQ_Payment_ReceiptNumber] UNIQUE ([ReceiptNumber]),
                CONSTRAINT [FK_Payment_Student] FOREIGN KEY ([StudentId]) REFERENCES [Student] ([StudentId]),
                CONSTRAINT [FK_Payment_Invoice] FOREIGN KEY ([InvoiceId]) REFERENCES [Invoice] ([InvoiceId])
            )
            """,
            "CREATE INDEX [IX_Payment_PaymentDate] ON [Payment] ([PaymentDate])"),

        new("PaymentChange", """
            CREATE TABLE [PaymentChange] (
                [PaymentChangeId] COUNTER CONSTRAINT [PK_PaymentChange] PRIMARY KEY,
                [PaymentId]       INTEGER NOT NULL,
                [ReceiptNumber]   TEXT(30) NOT NULL,
                [StudentId]       INTEGER NOT NULL,
                [ChangeType]      TEXT(10) NOT NULL,
                [ChangedDate]     DATETIME NOT NULL,
                [Details]         TEXT(255) NOT NULL,
                [Reason]          TEXT(255),
                CONSTRAINT [FK_PaymentChange_Student] FOREIGN KEY ([StudentId]) REFERENCES [Student] ([StudentId])
            )
            """,
            "CREATE INDEX [IX_PaymentChange_PaymentId] ON [PaymentChange] ([PaymentId])"),

        new("EmailHistory", """
            CREATE TABLE [EmailHistory] (
                [EmailHistoryId] COUNTER CONSTRAINT [PK_EmailHistory] PRIMARY KEY,
                [StudentId]      INTEGER NOT NULL,
                [InvoiceId]      INTEGER,
                [RecipientEmail] TEXT(150) NOT NULL,
                [EmailType]      TEXT(30) NOT NULL,
                [Subject]        TEXT(255),
                [SentDate]       DATETIME NOT NULL,
                [Status]         TEXT(20) NOT NULL,
                [ErrorMessage]   TEXT(255),
                CONSTRAINT [FK_EmailHistory_Student] FOREIGN KEY ([StudentId]) REFERENCES [Student] ([StudentId]),
                CONSTRAINT [FK_EmailHistory_Invoice] FOREIGN KEY ([InvoiceId]) REFERENCES [Invoice] ([InvoiceId])
            )
            """),

        new("Attendance", """
            CREATE TABLE [Attendance] (
                [AttendanceId]    COUNTER CONSTRAINT [PK_Attendance] PRIMARY KEY,
                [StudentId]       INTEGER NOT NULL,
                [AttendanceDate]  DATETIME NOT NULL,
                [IsPresent]       BIT NOT NULL,
                [Remarks]         TEXT(255),
                [MarkedDate]      DATETIME NOT NULL,
                [ParentEmailedDate] DATETIME,
                CONSTRAINT [UQ_Attendance_StudentDate] UNIQUE ([StudentId], [AttendanceDate]),
                CONSTRAINT [FK_Attendance_Student] FOREIGN KEY ([StudentId]) REFERENCES [Student] ([StudentId])
            )
            """,
            "CREATE INDEX [IX_Attendance_AttendanceDate] ON [Attendance] ([AttendanceDate])"),

        new("AdminUser", """
            CREATE TABLE [AdminUser] (
                [AdminUserId]  COUNTER CONSTRAINT [PK_AdminUser] PRIMARY KEY,
                [UserName]     TEXT(50) NOT NULL,
                [PasswordHash] TEXT(255) NOT NULL,
                [Email]        TEXT(150),
                [Phone]        TEXT(20),
                [CreatedDate]  DATETIME NOT NULL,
                [UpdatedDate]  DATETIME,
                CONSTRAINT [UQ_AdminUser_UserName] UNIQUE ([UserName])
            )
            """),

        new("AppSetting", """
            CREATE TABLE [AppSetting] (
                [SettingKey]   TEXT(50) CONSTRAINT [PK_AppSetting] PRIMARY KEY,
                [SettingValue] MEMO
            )
            """),
    ];
}
