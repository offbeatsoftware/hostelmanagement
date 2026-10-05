namespace HostelManagement.Data;

/// <summary>A table and the DDL statements that create it (CREATE TABLE first, then indexes).</summary>
public sealed record TableDefinition(string Name, params string[] Statements);

/// <summary>
/// The Access database schema. Tables are listed so that a table is always created
/// after the tables it references. See docs/DatabaseSchema.md for the design notes.
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
    public const int Version = 2;

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
                [UpdatedDate]    DATETIME
            )
            """),

        new("College", """
            CREATE TABLE [College] (
                [CollegeId]   COUNTER CONSTRAINT [PK_College] PRIMARY KEY,
                [CollegeName] TEXT(150) NOT NULL,
                [Address]     TEXT(255),
                [Phone]       TEXT(20),
                CONSTRAINT [UQ_College_CollegeName] UNIQUE ([CollegeName])
            )
            """),

        new("Room", """
            CREATE TABLE [Room] (
                [RoomId]      COUNTER CONSTRAINT [PK_Room] PRIMARY KEY,
                [RoomNumber]  TEXT(20) NOT NULL,
                [Floor]       TEXT(20),
                [Capacity]    INTEGER NOT NULL,
                [SharingType] TEXT(20) NOT NULL,
                [Rent]        CURRENCY NOT NULL,
                [IsActive]    BIT NOT NULL,
                [Remarks]     TEXT(255),
                CONSTRAINT [UQ_Room_RoomNumber] UNIQUE ([RoomNumber])
            )
            """),

        new("Student", """
            CREATE TABLE [Student] (
                [StudentId]     COUNTER CONSTRAINT [PK_Student] PRIMARY KEY,
                [StudentName]   TEXT(150) NOT NULL,
                [DateOfBirth]   DATETIME,
                [Gender]        TEXT(20),
                [Address]       TEXT(255),
                [CollegeId]     INTEGER,
                [Course]        TEXT(100),
                [ClassName]     TEXT(50),
                [Mobile]        TEXT(20),
                [Email]         TEXT(150),
                [PhotoPath]     TEXT(255),
                [AadhaarLast4]  TEXT(4),
                [AdmissionDate] DATETIME,
                [Status]        TEXT(20) NOT NULL,
                [Remarks]       TEXT(255),
                CONSTRAINT [FK_Student_College] FOREIGN KEY ([CollegeId]) REFERENCES [College] ([CollegeId])
            )
            """,
            "CREATE INDEX [IX_Student_StudentName] ON [Student] ([StudentName])",
            "CREATE INDEX [IX_Student_Mobile] ON [Student] ([Mobile])"),

        new("Parent", """
            CREATE TABLE [Parent] (
                [ParentId]         COUNTER CONSTRAINT [PK_Parent] PRIMARY KEY,
                [StudentId]        INTEGER NOT NULL,
                [ParentName]       TEXT(150) NOT NULL,
                [Relationship]     TEXT(50),
                [Mobile]           TEXT(20),
                [Email]            TEXT(150),
                [Address]          TEXT(255),
                [IsPrimaryContact] BIT NOT NULL,
                CONSTRAINT [FK_Parent_Student] FOREIGN KEY ([StudentId]) REFERENCES [Student] ([StudentId])
            )
            """),

        new("RoomAllocation", """
            CREATE TABLE [RoomAllocation] (
                [AllocationId] COUNTER CONSTRAINT [PK_RoomAllocation] PRIMARY KEY,
                [StudentId]    INTEGER NOT NULL,
                [RoomId]       INTEGER NOT NULL,
                [CheckInDate]  DATETIME NOT NULL,
                [CheckOutDate] DATETIME,
                [Status]       TEXT(20) NOT NULL,
                [Remarks]      TEXT(255),
                CONSTRAINT [FK_RoomAllocation_Student] FOREIGN KEY ([StudentId]) REFERENCES [Student] ([StudentId]),
                CONSTRAINT [FK_RoomAllocation_Room] FOREIGN KEY ([RoomId]) REFERENCES [Room] ([RoomId])
            )
            """,
            "CREATE INDEX [IX_RoomAllocation_Status] ON [RoomAllocation] ([Status])"),

        new("Service", """
            CREATE TABLE [Service] (
                [ServiceId]   COUNTER CONSTRAINT [PK_Service] PRIMARY KEY,
                [ServiceName] TEXT(100) NOT NULL,
                [Rate]        CURRENCY NOT NULL,
                [IsActive]    BIT NOT NULL,
                CONSTRAINT [UQ_Service_ServiceName] UNIQUE ([ServiceName])
            )
            """),

        new("Invoice", """
            CREATE TABLE [Invoice] (
                [InvoiceId]     COUNTER CONSTRAINT [PK_Invoice] PRIMARY KEY,
                [InvoiceNumber] TEXT(30) NOT NULL,
                [StudentId]     INTEGER NOT NULL,
                [InvoiceDate]   DATETIME NOT NULL,
                [BillingFrom]   DATETIME NOT NULL,
                [BillingTo]     DATETIME NOT NULL,
                [TotalAmount]   CURRENCY NOT NULL,
                CONSTRAINT [UQ_Invoice_InvoiceNumber] UNIQUE ([InvoiceNumber]),
                CONSTRAINT [FK_Invoice_Student] FOREIGN KEY ([StudentId]) REFERENCES [Student] ([StudentId])
            )
            """,
            "CREATE INDEX [IX_Invoice_InvoiceDate] ON [Invoice] ([InvoiceDate])"),

        new("InvoiceItem", """
            CREATE TABLE [InvoiceItem] (
                [InvoiceItemId] COUNTER CONSTRAINT [PK_InvoiceItem] PRIMARY KEY,
                [InvoiceId]     INTEGER NOT NULL,
                [Description]   TEXT(150) NOT NULL,
                [Quantity]      INTEGER NOT NULL,
                [Rate]          CURRENCY NOT NULL,
                [Amount]        CURRENCY NOT NULL,
                CONSTRAINT [FK_InvoiceItem_Invoice] FOREIGN KEY ([InvoiceId]) REFERENCES [Invoice] ([InvoiceId])
            )
            """),

        new("Payment", """
            CREATE TABLE [Payment] (
                [PaymentId]     COUNTER CONSTRAINT [PK_Payment] PRIMARY KEY,
                [StudentId]     INTEGER NOT NULL,
                [InvoiceId]     INTEGER,
                [PaymentDate]   DATETIME NOT NULL,
                [Amount]        CURRENCY NOT NULL,
                [PaymentMethod] TEXT(30) NOT NULL,
                [Reference]     TEXT(100),
                [Remarks]       TEXT(255),
                CONSTRAINT [FK_Payment_Student] FOREIGN KEY ([StudentId]) REFERENCES [Student] ([StudentId]),
                CONSTRAINT [FK_Payment_Invoice] FOREIGN KEY ([InvoiceId]) REFERENCES [Invoice] ([InvoiceId])
            )
            """,
            "CREATE INDEX [IX_Payment_PaymentDate] ON [Payment] ([PaymentDate])"),

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
    ];
}
