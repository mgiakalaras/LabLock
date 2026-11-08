# Ανέβασμα του LabLock στο GitHub

Αποθετήριο: https://github.com/mgiakalaras/LabLock

## Με ένα διπλό κλικ

1. Αν δεν έχεις Git: https://git-scm.com/download/win (προεπιλογές) ή PowerShell `winget install Git.Git`.
2. Διπλό κλικ στο **`ANEBASMA-STO-GITHUB.bat`** (μέσα στον φάκελο του LabLock).
   - **Μόνο την πρώτη φορά:** ανοίγει η σελίδα δημιουργίας αποθετηρίου. Όνομα `LabLock`, **Public**,
     **χωρίς** README / .gitignore / license → *Create repository* → γύρνα στο παράθυρο και πάτα ένα πλήκτρο.
     Αν ζητηθεί σύνδεση: *Sign in with your browser* → Authorize.
   - Στο τέλος γράφει «Έτοιμο!».

Το script κατεβάζει το αποθετήριο σε προσωρινό φάκελο, βάζει από πάνω τα αρχεία σου, κάνει commit και push.
Δεν ανεβαίνουν ποτέ το `LabLock.exe`, το `LabLock.ini`, το αρχείο των PC (`LabLock-computers.csv`),
οι εξαγωγές για Veyon / NetHawk και τα logs: είναι στοιχεία του σχολείου.

## Νέα έκδοση

1. Άλλαξε την έκδοση στο `LabLock\Properties\AssemblyInfo.cs` (και στο `LabLock\app.manifest`), π.χ. `0.5.1.0`.
2. Διπλό κλικ στο `ANEBASMA-STO-GITHUB.bat`.
3. Σε 2-3 λεπτά το GitHub έχει χτίσει το `LabLock.exe` (έκδοση `v0.5.1`)
   και το έχει βάλει στη σελίδα **Releases**, έτοιμο για λήψη από όποιον συνάδελφο το θέλει.

Αν ανεβάσεις αλλαγές χωρίς να αλλάξεις έκδοση, ανεβαίνουν μόνο τα αρχεία (χωρίς νέο Release).

## Έλεγχος

Καρτέλα **Actions** του αποθετηρίου: πράσινο ✔ = χτίστηκε σωστά.
Κόκκινο ✖ = λάθος στον κώδικα. Άνοιξέ το, και στο βήμα «Build» φαίνονται οι γραμμές του λάθους (ίδιες με το `build-log.txt`).
