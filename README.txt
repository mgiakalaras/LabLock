LabLock 0.5.0 - lock down the student account on school lab PCs
==============================================================

A small Windows program you run from a USB stick on each lab PC. You tick the
settings you want, press Apply, and it shows what is currently set on that PC.
It works on Windows 10 and 11, needs nothing installed, and speaks English and
Greek. The full guide is inside the program, on the Help page.


BUILD IT (once)
---------------
Before unpacking the zip: right-click it > Properties > tick "Unblock" > OK.

Either
  - double-click build.cmd (no Visual Studio needed). LabLock.exe appears next
    to it. If the build fails, the errors are in build-log.txt.
or
  - open LabLock.sln in Visual Studio, choose "Release", Build > Build Solution.
    The program is LabLock\bin\Release\LabLock.exe

Copy LabLock.exe to the USB stick. That one file is the whole program: the
school image and the push script are built into it, and it needs nothing
installed (it uses the .NET Framework that is part of Windows 10 and 11).


FIRST USE
---------
1. Run LabLock.exe on ONE lab PC and accept the administrator prompt.
2. Choose the student account, type the administrator account and a password.
3. Tick the settings, press "Apply ticked".
4. Sign out and check that the administrator account can sign in.
5. Check that the programs of the other subjects still work as the student.
Then do the other PCs. "Undo ticked" takes the ticked settings back.


RESTORE A PC
------------
"Restore this PC..." (in the THIS PC box) takes back everything LabLock has
changed on that PC, whatever is ticked. Each PC keeps its own record of what
LabLock did there (and what it was like before), so it puts back:
  - the earlier fixed IP address or DHCP, the earlier DNS servers,
  - the earlier computer name (after a restart),
  - admin rights for the student only if LabLock took them away,
  - the earlier account lockout rule, pictures, folders and policies.
Accounts made by LabLock are not deleted; restore points stay.
From the teacher PC:  Push-LabLock.ps1 -Restore  (or LabLock.exe /restore);
there the fixed IP and remote management are left on, so the connection stays.


WHAT IS NEW IN 0.4
------------------
Pages      Lockdown, This PC, Startup, Health check, Help, About.
Language   English / Greek (top right).
Settings   restore point first; lock an account after wrong passwords;
           block running programs from Downloads, Desktop and USB sticks;
           empty Downloads and Temp at sign-in; a browser group (forget on
           close, no saved passwords, no private or guest windows, no
           extensions, safe search); school image as account picture and lock
           screen; DNS only through your DNS servers (firewall).
This PC    hardware of the PC, room and notes in the record, export for Veyon
           and for NetHawk.
Startup    switch startup programs off and on, for all accounts and for the
           student. Nothing is deleted.
Health     a read-only check: administrators, antivirus and its exclusions,
           firewall, updates, disk, programs inside the student's account.
Accounts   "New..." creates a student account on a PC that has none.
Silent     LabLock.exe /apply  runs without a window, for many PCs at once.
0.4.2      "Restore this PC..." and /restore: take back everything LabLock did.
           "Prepare this PC for Wake-on-LAN" and "Wake up the PCs of the record"
           (This PC page), /wake and Push-LabLock.ps1 -Wake: switch the lab on.
           "Shut the PC down at night" (00:00, shutdown= in LabLock.ini) and the
           Boards page: a daily shut-down for interactive boards (boardshutdown=).
0.5.0      School Network Manager link (This PC page): every Apply, Undo and Restore
           is shown on the PC's page in SNM. GitHub: ANEBASMA-STO-GITHUB.bat.


FILES LABLOCK WRITES NEXT TO ITSELF
-----------------------------------
  LabLock.ini              your names, options, ticks, theme and language
  LabLock-blocklist.txt    the web sites to block
  LabLock-school.png       only if you put it there: replaces the built-in school image
  LabLock-computers.csv    the record of your PCs
  LabLock-log.txt          what was done on which PC and when
Passwords are never written to any of them.


FOR WHOEVER CHANGES THE CODE
----------------------------
  Settings.cs, MoreSettings.cs   the checklist. A simple policy is one entry in
                                 Catalog.Build(): id, title, hint, registry values.
  Lang.cs                        the Greek texts, as pairs: English, Greek.
  Texts.cs                       the Help and About pages.
  Ui.cs                          colours; an accent is one name and one RRGGBB.
