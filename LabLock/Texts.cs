namespace LabLock
{
    /// <summary>The long texts: the Help page and the About page, in English and in Greek.</summary>
    internal static class Texts
    {
        public const string GitHubUrl = "https://github.com/mgiakalaras";

        public const string AuthorName = "Marios Giakalaras  /  Μάριος Γιακαλάρας";

        public static string AuthorRole
        {
            get
            {
                return L.Greek
                    ? "Εκπαιδευτικός Πληροφορικής ΠΕ86, υπεύθυνος εργαστηρίου Πληροφορικής στο Μαυρογένειο ΕΠΑΛ Σάμου."
                    : "PE86 Informatics teacher and computer lab manager at the Mavrogeneio EPAL of Samos, Greece.";
            }
        }

        public static string AuthorBio
        {
            get
            {
                return L.Greek
                    ? "Ο Μάριος Γιακαλάρας είναι εκπαιδευτικός Πληροφορικής ΠΕ86, με μεταπτυχιακό και διδακτορικό από το Τμήμα Πολιτισμικής Τεχνολογίας και Επικοινωνίας του Πανεπιστημίου Αιγαίου. Φτιάχνει μικρά, πρακτικά εργαλεία για την πληροφορική του σχολείου, τη συντήρηση των Windows, τα δίκτυα και την τεχνολογία της τάξης. Το LabLock ξεκίνησε στο δικό του εργαστήριο: έντεκα υπολογιστές, ένας κοινός λογαριασμός μαθητή και καθόλου χρόνος για να παρακολουθείς κάθε οθόνη."
                    : "Marios Giakalaras is a Greek PE86 Informatics teacher with an MSc and a PhD from the Department of Cultural Technology and Communication of the University of the Aegean. He builds small, practical tools for school IT work, Windows maintenance, networks and classroom technology. LabLock started in his own lab: eleven PCs, one shared student account and no time to watch every screen.";
            }
        }

        public static string AboutApp
        {
            get
            {
                return L.Greek
                    ? "Το LabLock κλειδώνει τον λογαριασμό των μαθητών στους υπολογιστές ενός σχολικού εργαστηρίου. Τρέχει από ένα USB, δεν εγκαθιστά τίποτα, δείχνει τι ισχύει ήδη σε κάθε PC και κάθε ρύθμισή του αναιρείται. Δεν παγώνει τον υπολογιστή: οι άλλες ειδικότητες συνεχίζουν να δουλεύουν με τα προγράμματά τους."
                    : "LabLock locks down the students' account on the PCs of a school computer lab. It runs from a USB stick, installs nothing, shows what is already set on each PC, and every setting can be undone. It does not freeze the computer: other subjects keep working with their own programs.";
            }
        }

        public static string Support
        {
            get
            {
                return L.Greek
                    ? "Δεν υπάρχουν κουμπιά δωρεών ή πληρωμών. Αν το βρίσκεις χρήσιμο, στήριξέ το στο GitHub: δώσε ένα αστέρι, μοιράσου το, ανέφερε σφάλματα ή πρότεινε βελτιώσεις."
                    : "There are no donation or payment buttons. If you find it useful, support it on GitHub: star it, share it, report bugs or suggest improvements.";
            }
        }

        public static string Help
        {
            get { return L.Greek ? HelpGreek : HelpEnglish; }
        }

        private const string HelpEnglish =
@"QUICK START

1. Run LabLock.exe on the lab PC and accept the administrator prompt.
2. On the Lockdown page choose the student account. The list shows the real account name and, in brackets, the name on the sign-in screen when they differ. If the PC has no account for students, press New...
3. Type the administrator account and its password (twice). The password is only needed when the account does not exist yet.
4. Tick the settings you want and press Apply ticked. Nothing changes before that.
5. Sign out (or restart) and check that the administrator account can sign in.

Do one PC first. When it behaves as you want, do the others: your names, options and ticks travel on the USB stick in LabLock.ini.

Undo ticked takes the ticked settings back. LabLock never deletes accounts.

RESTORE THIS PC

Restore this PC... (in the THIS PC box) takes back everything LabLock has changed on this PC, whatever is ticked. Every PC keeps its own record of what LabLock did there and of what it was like before, so the PC gets back its earlier fixed IP address (or DHCP), DNS servers, computer name (after a restart) and lockout rule. The student account becomes an administrator again only if LabLock took the rights away. Accounts made by LabLock and restore points stay. If something fails, it stays in the record and the next Restore tries it again.


THE PAGES

Lockdown       the checklist. The line under each setting shows what the PC has now.
This PC        what the PC is made of, and the record of all your PCs.
Startup        the programs that start by themselves; switch them off without deleting anything.
Health check   a read-only look at what usually goes wrong on a lab PC.
Refresh        (top right) reads the open page again.


GOOD TO KNOW ABOUT SOME SETTINGS

Make the student a standard user
    The setting that matters most. It is only done when another working administrator account exists.

Lock the look of Windows / wallpaper / taskbar
    The locks keep the look the student account has at that moment. Put colours and icons right first.

Block running programs from Downloads and Desktop
    Stops the easy way: double-clicking a downloaded game. A student who moves the file to another folder can still run it. Check afterwards that the programs of the other subjects still start.

Empty Downloads and Temp at every sign-in
    Deletes for real. Documents, Desktop and Pictures are never touched.

Browsers
    They count for every account on the PC. ""Forget everything"" also throws away the students' bookmarks.

Fixed IP address
    LabLock first asks the network whether another device answers on the address. Undo puts back the address the PC had before LabLock: its earlier fixed address, or an automatic one (DHCP).

Wake-on-LAN
    Prepare this PC for Wake-on-LAN sets the wired network card and turns off Fast Startup (the BIOS setting must be on). Then, on the teacher PC, This PC > Wake up the PCs of the record switches on every PC of the record. Veyon's Power on button works too, because the Veyon export has the MAC addresses. Shut the PCs down normally; a PC that lost power needs to be switched on by hand once.

Night shut-down
    Every night (00:00 unless shutdown= in LabLock.ini says otherwise) a PC that is still on shows a 5-minute warning and shuts down. Anybody signed in can still save their work in those 5 minutes.

Boards page (interactive boards)
    For the Windows 11 module of an interactive board, which teachers use. Run LabLock on the board, type the time and press Apply on this board: every day at that time Windows warns for 5 minutes and shuts down. The screen and the Android side are not reachable from Windows: set the board's own scheduled power-off once, as the page explains.

School Network Manager
    On the This PC page, School Network Manager... takes the address of SNM (for example http://192.168.2.114:5055) and its agent key (SNM: Tools > Computer agent > Show). After that, every Apply, Undo and Restore sends SNM what is locked on the PC, and SNM shows it on the PC's page. The address and key stay in LabLock.ini, so all PCs get them from the USB stick. Without them LabLock works as before.

Rename this PC
    Always runs last; LabLock then offers to restart.


THE SCHOOL IMAGE

The school image is built into LabLock.exe and is used for the picture of every account and for the lock screen. For another school, choose another image on the Lockdown page, or put a file named LabLock-school.png next to LabLock.exe: it is used instead of the built-in one. Before anything is applied, LabLock tells you which image it is going to use. A logo is placed on white; a wide photo fills the lock screen.


THE BLOCKLIST

LabLock-blocklist.txt holds one site per line. It is written into each PC (hosts file and the browsers' own block lists), so it works without the USB stick and without the Pi-hole.
    example.com              the site, and all its sub-sites in the browsers
    example.com/some/page    only that part of the site (browsers only)
After changing the file, apply the blocklist setting again on each PC.


IF THE PI-HOLE IS DOWN

A PC that only knows the Pi-hole as DNS server has no internet when the Pi-hole is off. Type a second DNS server that also filters, for example 94.140.14.15 (AdGuard family protection) or 1.1.1.3 (Cloudflare for Families). Windows uses it only when the first one does not answer.
The big lists of the Pi-hole (hundreds of thousands of names) do not fit in the hosts file of a PC; the second DNS server does that job. The game sites of your blocklist stay blocked locally in any case.
The firewall setting ""Allow DNS only through the typed servers"" keeps both servers allowed and blocks every other DNS.


THE RECORD OF PCs

LabLock-computers.csv has one line per PC: name, IP address, fixed or automatic, mask, gateway, DNS, MAC address, Windows, room, hardware and your notes. It is updated after every Apply and opens in Excel. Close it in Excel before you go to the next PC.

Export for Veyon writes LabLock-veyon.csv. On the teacher's PC run, as administrator:
    veyon-cli networkobjects import LabLock-veyon.csv format ""%location%;%name%;%host%;%mac%""

Export for NetHawk writes LabLock-nethawk-machines.json in the format of NetHawk's machine inventory.


MANY PCs AT ONCE (SILENT RUN)

    LabLock.exe /apply            applies the settings that are ticked in LabLock.ini
    LabLock.exe /undo             takes them back
    LabLock.exe /restore          takes back everything LabLock did on the PC (not the fixed IP and remote management)
    LabLock.exe /wake             switches on every PC of the record (Wake-on-LAN)
    optional:  /student:NAME  /adminpass:PASSWORD  /ip:ADDRESS  /name:NEWNAME  /restart

There is no window; the result is written to LabLock-log.txt. The fixed IP and the rename are skipped unless /ip: and /name: are given. Push-LabLock.ps1 -Restore does /restore on every PC. The script Push-LabLock.ps1 copies LabLock to every PC of the record and runs it there; the button under this text saves it next to LabLock.exe. It needs ""Allow remote management"" to have been applied on the PCs first.


WHAT LABLOCK CANNOT DO FOR YOU

- A BIOS password and starting only from the disk. Without them a student with a USB stick can reset any password.
- A disk image of one clean PC (Clonezilla or similar), kept on your server. That is what brings a broken PC back in minutes.
- A browser with a built-in VPN, or a phone used as a hotspot, gets around every filter.


ONE FILE

LabLock.exe is the whole program: the school image and the push script are inside it. Copy that one file to the USB stick. While you work, LabLock writes these small files next to itself:

LabLock.ini              your names, options, ticks, theme and language. Never a password.
LabLock-blocklist.txt    the sites to block
LabLock-computers.csv    the record of PCs
LabLock-log.txt          what was done, on which PC, and when
LabLock-school.png       only if you put it there yourself: it replaces the built-in school image
";

        private const string HelpGreek =
@"ΓΡΗΓΟΡΗ ΕΚΚΙΝΗΣΗ

1. Τρέξε το LabLock.exe στο PC του εργαστηρίου και δέξου το παράθυρο διαχειριστή.
2. Στη σελίδα Κλείδωμα διάλεξε τον λογαριασμό μαθητή. Η λίστα δείχνει το πραγματικό όνομα του λογαριασμού και, σε παρένθεση, το όνομα της οθόνης σύνδεσης όταν διαφέρουν. Αν το PC δεν έχει λογαριασμό για μαθητές, πάτησε Νέος...
3. Γράψε τον λογαριασμό διαχειριστή και τον κωδικό του (δύο φορές). Ο κωδικός χρειάζεται μόνο όταν ο λογαριασμός δεν υπάρχει ακόμη.
4. Τσέκαρε τις ρυθμίσεις που θέλεις και πάτησε Εφαρμογή επιλεγμένων. Πριν από αυτό δεν αλλάζει τίποτα.
5. Αποσυνδέσου (ή κάνε επανεκκίνηση) και έλεγξε ότι ο λογαριασμός διαχειριστή συνδέεται.

Κάνε πρώτα ένα PC. Όταν συμπεριφέρεται όπως θέλεις, κάνε και τα υπόλοιπα: τα ονόματα, οι επιλογές και τα τσεκ σου ταξιδεύουν στο USB, μέσα στο LabLock.ini.

Η Αναίρεση επιλεγμένων παίρνει πίσω τις τσεκαρισμένες ρυθμίσεις. Το LabLock δεν διαγράφει ποτέ λογαριασμούς.

ΕΠΑΝΑΦΟΡΑ ΑΥΤΟΥ ΤΟΥ PC

Η Επαναφορά PC... (στο πλαίσιο ΑΥΤΟ ΤΟ PC) παίρνει πίσω ό,τι έχει αλλάξει το LabLock σε αυτό το PC, ανεξάρτητα από τα τσεκ. Κάθε PC κρατά δικό του αρχείο με το τι έκανε εκεί το LabLock και πώς ήταν πριν, οπότε το PC ξαναπαίρνει την προηγούμενη σταθερή IP (ή DHCP), τους DNS, το όνομά του (μετά από επανεκκίνηση) και τον κανόνα κλειδώματος. Ο λογαριασμός μαθητή ξαναγίνεται διαχειριστής μόνο αν του αφαίρεσε τα δικαιώματα το LabLock. Οι λογαριασμοί που έφτιαξε το LabLock και τα σημεία επαναφοράς μένουν. Ό,τι αποτύχει μένει στο αρχείο και η επόμενη Επαναφορά το ξαναδοκιμάζει.


ΟΙ ΣΕΛΙΔΕΣ

Κλείδωμα          η λίστα ρυθμίσεων. Η γραμμή κάτω από κάθε ρύθμιση δείχνει τι έχει τώρα το PC.
Αυτό το PC        από τι αποτελείται το PC και το αρχείο όλων των PC σου.
Εκκίνηση          τα προγράμματα που ξεκινούν μόνα τους· τα απενεργοποιείς χωρίς να σβήσεις κάτι.
Έλεγχος υγείας    μια ματιά, μόνο ανάγνωση, σε όσα συνήθως πάνε στραβά σε PC εργαστηρίου.
Ανανέωση          (πάνω δεξιά) ξαναδιαβάζει την ανοιχτή σελίδα.


ΚΑΛΟ ΝΑ ΞΕΡΕΙΣ ΓΙΑ ΚΑΠΟΙΕΣ ΡΥΘΜΙΣΕΙΣ

Ο μαθητής γίνεται τυπικός χρήστης
    Η ρύθμιση που μετράει περισσότερο. Γίνεται μόνο όταν υπάρχει άλλος λειτουργικός λογαριασμός διαχειριστή.

Κλείδωμα εμφάνισης / ταπετσαρίας / γραμμής εργασιών
    Τα κλειδώματα κρατούν την εμφάνιση που έχει ο λογαριασμός μαθητή εκείνη τη στιγμή. Φτιάξε πρώτα χρώματα και εικονίδια.

Αποκλεισμός εκτέλεσης από Λήψεις και Επιφάνεια εργασίας
    Κόβει τον εύκολο δρόμο: το διπλό κλικ σε ένα παιχνίδι που κατέβηκε. Ένας μαθητής που μεταφέρει το αρχείο σε άλλον φάκελο μπορεί ακόμη να το τρέξει. Έλεγξε μετά ότι τα προγράμματα των άλλων ειδικοτήτων ξεκινούν κανονικά.

Άδειασμα Λήψεων και προσωρινών σε κάθε σύνδεση
    Διαγράφει οριστικά. Τα Έγγραφα, η Επιφάνεια εργασίας και οι Εικόνες δεν πειράζονται ποτέ.

Περιηγητές
    Ισχύουν για κάθε λογαριασμό του PC. Το «ξεχνούν τα πάντα» σβήνει και τους σελιδοδείκτες των μαθητών.

Σταθερή διεύθυνση IP
    Το LabLock ρωτά πρώτα το δίκτυο αν απαντά άλλη συσκευή σε αυτή τη διεύθυνση. Η Αναίρεση βάζει πίσω τη διεύθυνση που είχε το PC πριν από το LabLock: την προηγούμενη σταθερή ή αυτόματη (DHCP).

Wake-on-LAN
    Η Προετοιμασία για Wake-on-LAN ρυθμίζει την ενσύρματη κάρτα δικτύου και κλείνει τη Γρήγορη εκκίνηση (η ρύθμιση στο BIOS πρέπει να είναι ενεργή). Μετά, στο PC του καθηγητή, το Αυτό το PC > Άνοιγμα των PC του αρχείου ανοίγει όλα τα PC του αρχείου. Δουλεύει και το κουμπί Ενεργοποίηση του Veyon, γιατί η εξαγωγή για Veyon έχει τις διευθύνσεις MAC. Κλείνε τα PC κανονικά· ένα PC που έμεινε χωρίς ρεύμα θέλει μία φορά να το ανοίξεις με το χέρι.

Αυτόματο κλείσιμο το βράδυ
    Κάθε βράδυ (στις 00:00, εκτός αν το shutdown= στο LabLock.ini λέει άλλη ώρα) ένα PC που είναι ακόμη ανοιχτό δείχνει προειδοποίηση 5 λεπτών και κλείνει. Όποιος είναι συνδεδεμένος προλαβαίνει να αποθηκεύσει τη δουλειά του.

Σελίδα Πίνακες (διαδραστικοί πίνακες)
    Για τη μονάδα Windows 11 ενός διαδραστικού πίνακα, που τη χρησιμοποιούν οι καθηγητές. Τρέξε το LabLock στον πίνακα, γράψε την ώρα και πάτησε Εφαρμογή σε αυτόν τον πίνακα: κάθε μέρα εκείνη την ώρα τα Windows προειδοποιούν για 5 λεπτά και κλείνουν. Η οθόνη και το Android δεν ελέγχονται από τα Windows: ρύθμισε μία φορά το προγραμματισμένο σβήσιμο του ίδιου του πίνακα, όπως εξηγεί η σελίδα.

School Network Manager
    Στη σελίδα Αυτό το PC, το School Network Manager... παίρνει τη διεύθυνση του SNM (π.χ. http://192.168.2.114:5055) και το κλειδί του agent (SNM: Εργαλεία > Agent υπολογιστών > Εμφάνιση). Μετά, κάθε Εφαρμογή, Αναίρεση και Επαναφορά στέλνει στο SNM τι είναι κλειδωμένο στο PC, και το SNM το δείχνει στη σελίδα του PC. Η διεύθυνση και το κλειδί μένουν στο LabLock.ini, οπότε όλα τα PC τα παίρνουν από το USB. Χωρίς αυτά το LabLock δουλεύει όπως πριν.

Μετονομασία του PC
    Τρέχει πάντα τελευταία· μετά το LabLock προτείνει επανεκκίνηση.


Η ΕΙΚΟΝΑ ΤΟΥ ΣΧΟΛΕΙΟΥ

Η εικόνα του σχολείου είναι ενσωματωμένη στο LabLock.exe και χρησιμοποιείται ως εικόνα κάθε λογαριασμού και στην οθόνη κλειδώματος. Για άλλο σχολείο, διάλεξε άλλη εικόνα στη σελίδα Κλείδωμα ή βάλε δίπλα στο LabLock.exe ένα αρχείο με όνομα LabLock-school.png: χρησιμοποιείται αυτό αντί για την ενσωματωμένη. Πριν εφαρμοστεί οτιδήποτε, το LabLock σου λέει ποια εικόνα θα χρησιμοποιήσει. Ένα λογότυπο μπαίνει σε λευκό φόντο· μια πλατιά φωτογραφία γεμίζει την οθόνη κλειδώματος.


Η ΛΙΣΤΑ ΑΠΟΚΛΕΙΣΜΟΥ

Το LabLock-blocklist.txt έχει έναν ιστότοπο ανά γραμμή. Γράφεται μέσα σε κάθε PC (αρχείο hosts και λίστες αποκλεισμού των περιηγητών), άρα δουλεύει χωρίς το USB και χωρίς το Pi-hole.
    example.com              ο ιστότοπος και, στους περιηγητές, όλοι οι υπο-ιστότοποί του
    example.com/some/page    μόνο αυτό το κομμάτι του ιστότοπου (μόνο περιηγητές)
Μετά από αλλαγή του αρχείου, εφάρμοσε ξανά τη ρύθμιση της λίστας σε κάθε PC.


ΑΝ ΠΕΣΕΙ ΤΟ PI-HOLE

Ένα PC που ξέρει μόνο το Pi-hole ως διακομιστή DNS μένει χωρίς internet όταν το Pi-hole είναι κλειστό. Γράψε και δεύτερο διακομιστή DNS που φιλτράρει, π.χ. 94.140.14.15 (AdGuard family protection) ή 1.1.1.3 (Cloudflare for Families). Τα Windows τον χρησιμοποιούν μόνο όταν ο πρώτος δεν απαντά.
Οι μεγάλες λίστες του Pi-hole (εκατοντάδες χιλιάδες ονόματα) δεν χωρούν στο αρχείο hosts ενός PC· αυτή τη δουλειά την κάνει ο δεύτερος διακομιστής DNS. Οι ιστότοποι παιχνιδιών της δικής σου λίστας μένουν αποκλεισμένοι τοπικά σε κάθε περίπτωση.
Η ρύθμιση του τείχους προστασίας «DNS μόνο μέσω των διακομιστών που έγραψες» αφήνει επιτρεπτούς και τους δύο και κόβει κάθε άλλο DNS.


ΤΟ ΑΡΧΕΙΟ ΤΩΝ PC

Το LabLock-computers.csv έχει μία γραμμή για κάθε PC: όνομα, διεύθυνση IP, σταθερή ή αυτόματη, μάσκα, πύλη, DNS, διεύθυνση MAC, Windows, αίθουσα, υλικό και τις σημειώσεις σου. Ενημερώνεται μετά από κάθε Εφαρμογή και ανοίγει στο Excel. Κλείσ' το στο Excel πριν πας στο επόμενο PC.

Η Εξαγωγή για Veyon γράφει το LabLock-veyon.csv. Στο PC του καθηγητή τρέξε, ως διαχειριστής:
    veyon-cli networkobjects import LabLock-veyon.csv format ""%location%;%name%;%host%;%mac%""

Η Εξαγωγή για NetHawk γράφει το LabLock-nethawk-machines.json, στη μορφή του καταλόγου μηχανημάτων του NetHawk.


ΠΟΛΛΑ PC ΜΑΖΙ (ΑΘΟΡΥΒΗ ΕΚΤΕΛΕΣΗ)

    LabLock.exe /apply            εφαρμόζει τις ρυθμίσεις που είναι τσεκαρισμένες στο LabLock.ini
    LabLock.exe /undo             τις παίρνει πίσω
    LabLock.exe /restore          παίρνει πίσω ό,τι έκανε το LabLock στο PC (όχι τη σταθερή IP και την απομακρυσμένη διαχείριση)
    LabLock.exe /wake             ανοίγει όλα τα PC του αρχείου (Wake-on-LAN)
    προαιρετικά:  /student:ΟΝΟΜΑ  /adminpass:ΚΩΔΙΚΟΣ  /ip:ΔΙΕΥΘΥΝΣΗ  /name:ΝΕΟ_ΟΝΟΜΑ  /restart

Δεν ανοίγει παράθυρο· το αποτέλεσμα γράφεται στο LabLock-log.txt. Η σταθερή IP και η μετονομασία παραλείπονται, εκτός αν δοθούν /ip: και /name:. Το Push-LabLock.ps1 -Restore κάνει /restore σε όλα τα PC. Το σενάριο Push-LabLock.ps1 αντιγράφει το LabLock σε κάθε PC του αρχείου και το τρέχει εκεί· το κουμπί κάτω από αυτό το κείμενο το αποθηκεύει δίπλα στο LabLock.exe. Χρειάζεται να έχει εφαρμοστεί πρώτα στα PC η «Απομακρυσμένη διαχείριση».


ΤΙ ΔΕΝ ΜΠΟΡΕΙ ΝΑ ΚΑΝΕΙ ΤΟ LABLOCK ΓΙΑ ΣΕΝΑ

- Κωδικός στο BIOS και εκκίνηση μόνο από τον δίσκο. Χωρίς αυτά, ένας μαθητής με ένα USB αλλάζει οποιονδήποτε κωδικό.
- Εικόνα δίσκου ενός καθαρού PC (Clonezilla ή κάτι αντίστοιχο), φυλαγμένη στον server σου. Αυτή επαναφέρει ένα χαλασμένο PC σε λίγα λεπτά.
- Ένας περιηγητής με ενσωματωμένο VPN ή ένα κινητό ως hotspot παρακάμπτει κάθε φίλτρο.


ΕΝΑ ΑΡΧΕΙΟ

Το LabLock.exe είναι ολόκληρο το πρόγραμμα: η εικόνα του σχολείου και το σενάριο push βρίσκονται μέσα του. Αντίγραψε μόνο αυτό το αρχείο στο USB. Όσο δουλεύεις, το LabLock γράφει δίπλα του αυτά τα μικρά αρχεία:

LabLock.ini              ονόματα, επιλογές, τσεκ, θέμα και γλώσσα. Ποτέ κωδικός.
LabLock-blocklist.txt    οι ιστότοποι προς αποκλεισμό
LabLock-computers.csv    το αρχείο των PC
LabLock-log.txt          τι έγινε, σε ποιο PC και πότε
LabLock-school.png       μόνο αν το βάλεις εσύ: αντικαθιστά την ενσωματωμένη εικόνα του σχολείου
";
    }
}
