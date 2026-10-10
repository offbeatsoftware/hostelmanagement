HOSTEL MANAGEMENT SYSTEM: INSTALLATION GUIDE
============================================

What the hostel PC needs
------------------------
- Windows 10 (version 1809 or later) or Windows 11, 64 bit.
- Optional: a webcam (built in or USB) for Take Photo on the student form.
- The Microsoft Access Database Engine, 64 bit. Microsoft Access itself is NOT needed.
  If the setup program reports that the engine is missing, download
  "Microsoft Access Database Engine 2016 Redistributable" from
  https://www.microsoft.com/en-us/download/details.aspx?id=54920
  and run accessdatabaseengine_X64.exe.
- .NET does not need to be installed: it is included in the setup program.

32 bit or 64 bit Office
-----------------------
The program is 64 bit and needs the 64 bit Access Database Engine. Most PCs with Microsoft 365 or
Office 2016 or later already have 64 bit Office, which works.
If 32 bit Office is installed, Windows refuses to install the 64 bit engine next to it. In that case
either change Office to the 64 bit version, or ask for a 32 bit build of this program.
(To check: open Word > File > Account > About Word; the first line ends with "32-bit" or "64-bit".)

Installing
----------
1. Run HostelManagement-Setup-<version>.exe and allow it to make changes (administrator).
2. Keep the folder C:\HostelManagement. Do not choose C:\Program Files: Windows does not let the
   program save its database there.
3. Tick "Create a desktop shortcut" and finish. The program starts.
4. Sign in with user name admin and password admin, then at once:
   - Settings > Admin Account: change the password, enter the admin's email and phone.
   - Hostels: add the hostel(s) and choose how often each hostel bills (twice or four times a year).
   - Rooms: enter the yearly rent per sharing type, then add the rooms.
   - Settings > Email Settings: enter the Gmail app password (see the user guide) and send a test email.
   - Settings > Backup / Restore: choose a second backup folder (USB drive, Google Drive or OneDrive).

Where the data is kept
----------------------
Everything is in C:\HostelManagement:
  Database\HostelManagement.accdb   all hostel data
  Photos, Documents                 student photos and Aadhaar cards
  Invoices, Receipts, Reports,
  Agreements                        PDF and Excel files created by the program
  Backups                           backup zips (one automatic backup a day, the last 30 kept)
  Logs                              technical error logs

Updating to a new version
-------------------------
Close the program, make a backup (Backup / Restore > Backup Now), then run the new setup program into
the same folder. The database and all data stay as they are.

Moving to a new PC
------------------
1. On the old PC: Backup / Restore > Backup Now, and copy the newest zip from C:\HostelManagement\Backups
   to a USB drive (or use the second backup folder).
2. On the new PC: install the program and the Access Database Engine, sign in with admin / admin.
3. Backup / Restore > Restore From File, choose the zip. The program restarts with all data.
4. Enter the Gmail app password again in Email Settings (it is stored encrypted for each Windows user
   and PC, so it does not move with the backup).

Uninstalling
------------
Windows Settings > Apps > Hostel Management System > Uninstall. The program is removed; the database,
photos, documents and backups in C:\HostelManagement are kept. Delete that folder by hand only when the
data is no longer needed.
