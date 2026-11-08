using System;
using System.Collections.Generic;

namespace LabLock
{
    /// <summary>
    /// The language switch. The whole program is written in English; a text becomes Greek at the moment it is
    /// shown, by passing through L.T(). A text that is in the table is replaced as a whole. A text that was put
    /// together while running (with a name or an address inside it) has its known parts replaced one by one.
    /// A text that is not in the table simply stays English, so a missing line is never an error.
    /// </summary>
    internal static class L
    {
        public static bool Greek;

        private static Dictionary<string, string> exact;
        private static List<KeyValuePair<string, string>> parts;
        private static readonly Dictionary<string, string> cache = new Dictionary<string, string>(StringComparer.Ordinal);

        private static void Load()
        {
            exact = new Dictionary<string, string>(StringComparer.Ordinal);
            parts = new List<KeyValuePair<string, string>>();

            string[] whole = GreekText.Texts;
            for (int i = 0; i + 1 < whole.Length; i += 2)
            {
                exact[whole[i]] = whole[i + 1];
                // long texts (titles, messages) may also appear inside a longer, put-together text
                if (whole[i].Length >= 14) parts.Add(new KeyValuePair<string, string>(whole[i], whole[i + 1]));
            }

            string[] pieces = GreekText.Parts;
            for (int i = 0; i + 1 < pieces.Length; i += 2)
            {
                parts.Add(new KeyValuePair<string, string>(pieces[i], pieces[i + 1]));
            }

            // longest first, so a long text is never broken up by a shorter one that it contains
            parts.Sort(delegate(KeyValuePair<string, string> a, KeyValuePair<string, string> b)
            {
                return b.Key.Length.CompareTo(a.Key.Length);
            });
        }

        public static string T(string text)
        {
            if (!Greek || string.IsNullOrEmpty(text)) return text;
            if (exact == null) Load();

            string found;
            if (exact.TryGetValue(text, out found)) return found;
            if (cache.TryGetValue(text, out found)) return found;

            string result = text;
            foreach (KeyValuePair<string, string> part in parts)
            {
                if (result.IndexOf(part.Key, StringComparison.Ordinal) >= 0)
                {
                    result = result.Replace(part.Key, part.Value);
                }
            }

            if (cache.Count > 3000) cache.Clear();
            cache[text] = result;
            return result;
        }

        /// <summary>The Greek text whatever the language on screen (for School Network Manager, which is in Greek).</summary>
        public static string ToGreek(string text)
        {
            bool before = Greek;
            Greek = true;
            try
            {
                return T(text);
            }
            finally
            {
                Greek = before;
            }
        }
    }

    /// <summary>
    /// The Greek texts. Each line is an English text exactly as it is written in the program, followed by its
    /// Greek. To correct a translation, change the second text of the pair. To translate a text that still
    /// shows in English, add a pair with that English text.
    /// </summary>
    internal static class GreekText
    {
        public const string LanguageName = "Ελληνικά";

        public static readonly string[] Texts =
        {
            // ---- the frame
            "school lab lockdown", "κλείδωμα σχολικού εργαστηρίου",
            "Lockdown", "Κλείδωμα",
            "This PC", "Αυτό το PC",
            "Startup", "Εκκίνηση",
            "Health check", "Έλεγχος υγείας",
            "Help", "Βοήθεια",
            "About", "Σχετικά",
            "Refresh", "Ανανέωση",
            "This PC: ", "Αυτό το PC: ",
            "Dark", "Σκούρο",
            "Light", "Ανοιχτό",
            "Lime", "Λάιμ",
            "Green", "Πράσινο",
            "Teal", "Πετρόλ",
            "Blue", "Μπλε",
            "Purple", "Μωβ",
            "Red", "Κόκκινο",
            "Orange", "Πορτοκαλί",
            "OK", "OK",
            "Cancel", "Άκυρο",
            "Ready. Nothing is changed until you press Apply.", "Έτοιμο. Τίποτα δεν αλλάζει μέχρι να πατήσεις Εφαρμογή.",
            "LabLock changes accounts and system settings, so it must run as administrator.\r\n\r\nRight-click LabLock.exe and choose \"Run as administrator\".",
                "Το LabLock αλλάζει λογαριασμούς και ρυθμίσεις συστήματος, γι' αυτό πρέπει να τρέχει ως διαχειριστής.\r\n\r\nΚάνε δεξί κλικ στο LabLock.exe και διάλεξε «Εκτέλεση ως διαχειριστής».",
            "Something went wrong:", "Κάτι πήγε στραβά:",

            // ---- Lockdown page: left column
            "ACCOUNTS", "ΛΟΓΑΡΙΑΣΜΟΙ",
            "Student account", "Λογαριασμός μαθητή",
            "(choose the student account)", "(διάλεξε τον λογαριασμό μαθητή)",
            "New...", "Νέος...",
            "Administrator", "Διαχειριστής",
            "Password, twice", "Κωδικός (x2)",
            "THIS PC", "ΑΥΤΟ ΤΟ PC",
            "Computer name", "Όνομα PC",
            "IP address", "Διεύθυνση IP",
            "Subnet mask", "Μάσκα",
            "Gateway", "Πύλη",
            "DNS servers", "DNS (1ος, 2ος)",
            "OPTIONS", "ΕΠΙΛΟΓΕΣ",
            "Wallpaper", "Ταπετσαρία",
            "School image", "Εικόνα σχολείου",
            "Browse...", "Επιλογή...",
            "Edit...", "Αλλαγή...",
            "Blocklist: ", "Λίστα: ",
            " game sites", " ιστότοποι",
            "Apply ticked", "Εφαρμογή επιλεγμένων",
            "Undo ticked", "Αναίρεση επιλεγμένων",
            "Tick the settings you want and press Apply. Point at a line to read what it does.",
                "Τσέκαρε τις ρυθμίσεις που θέλεις και πάτησε Εφαρμογή. Δείξε με το ποντίκι μια γραμμή για να διαβάσεις τι κάνει.",
            "Images", "Εικόνες",
            "All files", "Όλα τα αρχεία",
            "Choose the wallpaper image", "Διάλεξε την εικόνα της ταπετσαρίας",
            "Choose the school image (logo or photo)", "Διάλεξε την εικόνα του σχολείου (λογότυπο ή φωτογραφία)",
            "New student account", "Νέος λογαριασμός μαθητή",
            "Name of the new account (for example Student)", "Όνομα του νέου λογαριασμού (π.χ. Student)",
            "Password (may be left empty)", "Κωδικός (μπορεί να μείνει κενός)",
            "An account with that name already exists. Choose it from the list.", "Υπάρχει ήδη λογαριασμός με αυτό το όνομα. Διάλεξέ τον από τη λίστα.",
            "Save the file in Notepad, then press Refresh. The new list is used the next time you apply the blocklist setting.",
                "Αποθήκευσε το αρχείο στο Σημειωματάριο και πάτησε Ανανέωση. Η νέα λίστα χρησιμοποιείται την επόμενη φορά που θα εφαρμόσεις τη ρύθμιση της λίστας αποκλεισμού.",
            "The blocklist file could not be created next to LabLock.exe (is the USB stick write-protected?).\r\n\r\nThe built-in starter list is used instead.",
                "Το αρχείο της λίστας αποκλεισμού δεν μπόρεσε να δημιουργηθεί δίπλα στο LabLock.exe (μήπως το USB είναι κλειδωμένο για εγγραφή;).\r\n\r\nΧρησιμοποιείται η ενσωματωμένη αρχική λίστα.",

            // ---- Lockdown page: checks before a run
            "Choose the student account first.", "Διάλεξε πρώτα τον λογαριασμό μαθητή.",
            "Type a name for the administrator account.", "Γράψε ένα όνομα για τον λογαριασμό διαχειριστή.",
            "The administrator account must be a different account from the student account.", "Ο λογαριασμός διαχειριστή πρέπει να είναι διαφορετικός από τον λογαριασμό μαθητή.",
            "Choose a password of at least 8 characters for the new administrator account.", "Διάλεξε κωδικό με τουλάχιστον 8 χαρακτήρες για τον νέο λογαριασμό διαχειριστή.",
            "The two passwords are not the same.", "Οι δύο κωδικοί δεν είναι ίδιοι.",
            "Tick 'Create a separate administrator account' as well, so the PC is not left without an administrator.",
                "Τσέκαρε και το «Δημιουργία ξεχωριστού λογαριασμού διαχειριστή», ώστε το PC να μη μείνει χωρίς διαχειριστή.",
            "The game sites blocklist is empty. Press Edit... and add at least one site.", "Η λίστα αποκλεισμού είναι άδεια. Πάτησε Αλλαγή... και πρόσθεσε τουλάχιστον έναν ιστότοπο.",
            "Tick at least one setting first.", "Τσέκαρε πρώτα τουλάχιστον μία ρύθμιση.",
            "Apply the ticked settings on ", "Εφαρμογή των επιλεγμένων ρυθμίσεων στο ",
            "Undo the ticked settings on ", "Αναίρεση των επιλεγμένων ρυθμίσεων στο ",
            "Student account: ", "Λογαριασμός μαθητή: ",
            "School image: ", "Εικόνα σχολείου: ",
            "the image built into LabLock", "η ενσωματωμένη εικόνα του LabLock",
            "Choose a school image first: this copy of LabLock has no built-in image.", "Διάλεξε πρώτα εικόνα σχολείου: αυτό το αντίγραφο του LabLock δεν έχει ενσωματωμένη εικόνα.",
            "Save Push-LabLock.ps1 next to LabLock.exe", "Αποθήκευση του Push-LabLock.ps1 δίπλα στο LabLock.exe",
            "This copy of LabLock was built without the push script.", "Αυτό το αντίγραφο του LabLock φτιάχτηκε χωρίς το σενάριο push.",
            "Fixed IP address: ", "Σταθερή διεύθυνση IP: ",
            "New computer name: ", "Νέο όνομα υπολογιστή: ",
            "The student's Downloads will be emptied at every sign-in.", "Οι Λήψεις του μαθητή θα αδειάζουν σε κάθε σύνδεση.",
            "The administrator account already exists and its password will NOT be changed. Make sure you know that password: afterwards it is the only way to administer this PC.",
                "Ο λογαριασμός διαχειριστή υπάρχει ήδη και ο κωδικός του ΔΕΝ θα αλλάξει. Βεβαιώσου ότι τον ξέρεις: μετά θα είναι ο μόνος τρόπος να διαχειριστείς αυτό το PC.",
            " done, ", " έγιναν, ",
            " failed.", " απέτυχαν.",
            "Read the lines marked FAILED in the log.", "Διάβασε στο ημερολόγιο τις γραμμές με την ένδειξη ΑΠΕΤΥΧΕ.",
            "The new computer name is used after a restart. The restart also makes all the other changes take hold.",
                "Το νέο όνομα υπολογιστή ισχύει μετά από επανεκκίνηση. Με την επανεκκίνηση εφαρμόζονται και όλες οι άλλες αλλαγές.",
            "After the restart, sign in with the administrator account to check that it works.", "Μετά την επανεκκίνηση, συνδέσου με τον λογαριασμό διαχειριστή για να δεις ότι δουλεύει.",
            "Restart this PC now?", "Επανεκκίνηση του PC τώρα;",
            "Before you go to the next PC: sign out and check that you can sign in with the administrator account.",
                "Πριν πας στο επόμενο PC: αποσυνδέσου και έλεγξε ότι μπορείς να συνδεθείς με τον λογαριασμό διαχειριστή.",
            "Settings for the student take effect the next time the student signs in.", "Οι ρυθμίσεις του μαθητή ισχύουν από την επόμενη σύνδεση του μαθητή.",
            "Restarting this PC.", "Γίνεται επανεκκίνηση του PC.",
            "The restart could not be started", "Η επανεκκίνηση δεν μπόρεσε να ξεκινήσει",
            "Restart the PC yourself from the Start menu.", "Κάνε επανεκκίνηση του PC από το μενού Έναρξη.",

            // ---- Wake-on-LAN
            "Prepare this PC for Wake-on-LAN (network card, Fast Startup off)", "Προετοιμασία για Wake-on-LAN (κάρτα δικτύου, χωρίς Γρήγορη εκκίνηση)",
            "So you can switch the PCs on from the teacher PC (This PC page, or Veyon's Power on). Sets the wired network card to wake on the magic packet, also after Shut down, and turns off Fast Startup. Wake-on-LAN must be on in the BIOS. Takes effect after the next restart.",
                "Για να ανοίγεις τα PC από το PC του καθηγητή (σελίδα Αυτό το PC ή «Ενεργοποίηση» του Veyon). Ρυθμίζει την ενσύρματη κάρτα δικτύου να ξυπνά με το magic packet, και μετά από Τερματισμό, και κλείνει τη Γρήγορη εκκίνηση. Το Wake-on-LAN πρέπει να είναι ενεργό στο BIOS. Ισχύει μετά την επόμενη επανεκκίνηση.",
            "Wake up the PCs of the record (Wake-on-LAN)", "Άνοιγμα των PC του αρχείου (Wake-on-LAN)",
            "The record has no PC with a MAC address yet. Run LabLock once on every lab PC (Apply, or Save this PC to the record), then bring the record here.",
                "Το αρχείο δεν έχει ακόμη PC με διεύθυνση MAC. Τρέξε μία φορά το LabLock σε κάθε PC του εργαστηρίου (Εφαρμογή ή Αποθήκευση στο αρχείο) και φέρε το αρχείο εδώ.",
            "Send a wake-up signal to the PCs of the record?", "Αποστολή σήματος αφύπνισης στα PC του αρχείου;",
            "A PC that stays off: check that Wake-on-LAN is on in its BIOS, that the setting \"Prepare this PC for Wake-on-LAN\" was applied and the PC restarted once, that it was shut down normally (not unplugged) and that this PC is in the same network.",
                "Αν κάποιο PC μείνει κλειστό, έλεγξε ότι: το Wake-on-LAN είναι ενεργό στο BIOS του, έχει εφαρμοστεί η «Προετοιμασία για Wake-on-LAN» και έγινε μία επανεκκίνηση, έκλεισε κανονικά (όχι από την πρίζα) και αυτό το PC είναι στο ίδιο δίκτυο.",
            "This PC is on Wi-Fi. Wake-on-LAN needs the network cable.", "Αυτό το PC είναι σε Wi-Fi. Το Wake-on-LAN θέλει καλώδιο δικτύου.",
            "Not applied (the PC is on Wi-Fi; Wake-on-LAN needs the cable)", "Δεν εφαρμόστηκε (το PC είναι σε Wi-Fi· το Wake-on-LAN θέλει καλώδιο)",
            "The settings of the network card were not found; only Fast Startup and waking are set.", "Δεν βρέθηκαν οι ρυθμίσεις της κάρτας δικτύου· ρυθμίστηκαν μόνο η Γρήγορη εκκίνηση και η αφύπνιση.",
            "LabLock has not prepared Wake-on-LAN on this PC, so nothing was changed.", "Το LabLock δεν έχει ρυθμίσει Wake-on-LAN σε αυτό το PC, οπότε δεν άλλαξε τίποτα.",
            "The network card and Fast Startup are back as they were before LabLock (after the next restart).", "Η κάρτα δικτύου και η Γρήγορη εκκίνηση γύρισαν όπως ήταν πριν από το LabLock (μετά την επόμενη επανεκκίνηση).",

            // ---- School Network Manager
            "Address of SNM (for example http://192.168.2.114:5055), empty = no link", "Διεύθυνση του SNM (π.χ. http://192.168.2.114:5055), κενό = χωρίς σύνδεση",
            "Agent key (SNM: Tools > Computer agent > Show)", "Κλειδί agent (SNM: Εργαλεία > Agent υπολογιστών > Εμφάνιση)",
            "The link to School Network Manager is off.", "Η σύνδεση με το School Network Manager έκλεισε.",
            "Linked. From now on every Apply, Undo and Restore on this PC is shown in School Network Manager, on the page of this PC. LabLock.ini keeps the address and key, so the other PCs get them from the USB stick.",
                "Συνδέθηκε. Από εδώ και πέρα κάθε Εφαρμογή, Αναίρεση και Επαναφορά σε αυτό το PC φαίνεται στο School Network Manager, στη σελίδα του PC. Το LabLock.ini κρατά τη διεύθυνση και το κλειδί, οπότε τα άλλα PC τα παίρνουν από το ίδιο USB.",
            "The key is wrong. Copy it from SNM: Tools > Computer agent > Show.", "Το κλειδί είναι λάθος. Αντέγραψέ το από το SNM: Εργαλεία > Agent υπολογιστών > Εμφάνιση.",
            "School Network Manager answered, but it is an older version without the LabLock link (update it to 2.17 or newer).",
                "Το School Network Manager απάντησε, αλλά είναι παλαιότερη έκδοση χωρίς σύνδεση με το LabLock (ενημέρωσέ το στην 2.17 ή νεότερη).",

            // ---- Boards page
            "Boards", "Πίνακες",
            "THE BOARD'S WINDOWS (WINDOWS 11 MODULE)", "ΤΑ WINDOWS ΤΟΥ ΠΙΝΑΚΑ (ΜΟΝΑΔΑ WINDOWS 11)",
            "Run LabLock on the board itself. Teachers often forget the board on: every day at this time it shows a 5-minute warning and shuts down. Nothing else of the lab lockdown is applied here.",
                "Τρέξε το LabLock στον ίδιο τον πίνακα. Συχνά ο πίνακας ξεχνιέται ανοιχτός: κάθε μέρα αυτή την ώρα δείχνει προειδοποίηση 5 λεπτών και κλείνει. Τίποτε άλλο από το κλείδωμα του εργαστηρίου δεν εφαρμόζεται εδώ.",
            "Shut down at", "Κλείσιμο στις",
            "(24-hour time, for example 21:00)", "(ώρα 24ώρου, π.χ. 21:00)",
            "Apply on this board", "Εφαρμογή σε αυτόν τον πίνακα",
            "Undo on this board", "Αναίρεση σε αυτόν τον πίνακα",
            "From the teacher PC, for many boards: LabLock.exe /apply /shutdown:21:00 (with only the night shut-down ticked).",
                "Από το PC του καθηγητή, για πολλούς πίνακες: LabLock.exe /apply /shutdown:21:00 (με τσεκαρισμένο μόνο το αυτόματο κλείσιμο).",
            "THE BOARD ITSELF (SCREEN AND ANDROID)", "Ο ΙΔΙΟΣ Ο ΠΙΝΑΚΑΣ (ΟΘΟΝΗ ΚΑΙ ANDROID)",
            "When Windows shuts down, only the Windows module is off: the screen and Android stay on. Set this once on every board, from its own menu (Android settings or the board's settings button):\r\n\r\n" +
            "1. A scheduled power-off. It is called Timer switch, Scheduled power off, Power schedule or Eco. Set it 10 minutes after the time on the left, every day.\r\n\r\n" +
            "2. If the board has it: switch off when there is no signal. Then the board also goes off by itself once Windows has shut down.\r\n\r\n" +
            "3. In the board's power options, choose that Windows starts with the board, so the next morning everything comes on with one button.",
                "Όταν κλείνουν τα Windows, σβήνει μόνο η μονάδα Windows: η οθόνη και το Android μένουν ανοιχτά. Ρύθμισε μία φορά σε κάθε πίνακα, από το δικό του μενού (ρυθμίσεις Android ή το κουμπί ρυθμίσεων του πίνακα):\r\n\r\n" +
                "1. Προγραμματισμένο σβήσιμο. Λέγεται Timer switch, Scheduled power off, Power schedule ή Eco. Βάλε το 10 λεπτά μετά την ώρα αριστερά, κάθε μέρα.\r\n\r\n" +
                "2. Αν το έχει: σβήσιμο όταν δεν υπάρχει σήμα. Τότε ο πίνακας σβήνει και μόνος του μόλις κλείσουν τα Windows.\r\n\r\n" +
                "3. Στις επιλογές ενέργειας του πίνακα, διάλεξε τα Windows να ανοίγουν μαζί με τον πίνακα, ώστε το πρωί όλα να ανοίγουν με ένα κουμπί.",
            "The time is kept in LabLock.ini (boardshutdown=). The lab PCs keep their own time (shutdown=, 00:00), set on the Lockdown page. Restore this PC takes the board's shut-down back too.",
                "Η ώρα φυλάσσεται στο LabLock.ini (boardshutdown=). Τα PC του εργαστηρίου έχουν τη δική τους ώρα (shutdown=, 00:00), από τη σελίδα Κλείδωμα. Η Επαναφορά PC αναιρεί και το κλείσιμο του πίνακα.",
            "Type the time as hours and minutes, for example 21:00.", "Γράψε την ώρα σε ώρες και λεπτά, π.χ. 21:00.",
            "Remove the daily shut-down from this board?", "Αφαίρεση του καθημερινού κλεισίματος από αυτόν τον πίνακα;",
            "Done. Now set the board's own power-off (right side of this page).", "Έγινε. Τώρα ρύθμισε και το σβήσιμο του ίδιου του πίνακα (δεξιά σε αυτή τη σελίδα).",
            "Done.", "Έγινε.",

            // ---- night shut-down
            "Shut the PC down at night if it is left on (00:00, with a warning)", "Αυτόματο κλείσιμο το βράδυ αν μείνει ανοιχτό (00:00, με προειδοποίηση)",
            "Every night the PC shows a 5-minute warning and then shuts down, so no PC stays on until the morning. A normal shut-down also keeps Wake-on-LAN working. The time can be changed in LabLock.ini (shutdown=00:00).",
                "Κάθε βράδυ το PC δείχνει προειδοποίηση 5 λεπτών και μετά κλείνει, ώστε κανένα PC να μη μένει ανοιχτό ως το πρωί. Το κανονικό κλείσιμο κρατά και το Wake-on-LAN σε λειτουργία. Η ώρα αλλάζει στο LabLock.ini (shutdown=00:00).",
            "Could not create the night shut-down task", "Δεν δημιουργήθηκε η εργασία νυχτερινού κλεισίματος",

            // ---- Restore this PC
            "Restore this PC...", "Επαναφορά PC...",
            "LabLock has not changed anything on this PC (there is nothing in its record of changes).\r\n\r\nSettings applied by hand or by an older LabLock can still be taken back with Undo ticked.",
                "Το LabLock δεν έχει αλλάξει τίποτα σε αυτό το PC (το αρχείο αλλαγών του είναι άδειο).\r\n\r\nΡυθμίσεις που έγιναν με το χέρι ή από παλαιότερο LabLock αναιρούνται με την Αναίρεση επιλεγμένων.",
            "Restore this PC to how it was before LabLock?", "Επαναφορά του PC όπως ήταν πριν από το LabLock;",
            "Computer: ", "Υπολογιστής: ",
            "These changes will be taken back:", "Θα αναιρεθούν αυτές οι αλλαγές:",
            "If LabLock took the administrator rights away from the student account, the student account becomes an administrator again.",
                "Αν το LabLock αφαίρεσε τα δικαιώματα διαχειριστή από τον λογαριασμό μαθητή, ο λογαριασμός μαθητή ξαναγίνεται διαχειριστής.",
            "The network address goes back to what it was before LabLock. If it changes, update it in Veyon.",
                "Η διεύθυνση δικτύου γυρίζει σε ό,τι ήταν πριν από το LabLock. Αν αλλάξει, διόρθωσέ τη στο Veyon.",
            "The computer name goes back to the earlier one after a restart.", "Το όνομα του PC γυρίζει στο προηγούμενο μετά από επανεκκίνηση.",
            "Accounts made by LabLock are not deleted.", "Οι λογαριασμοί που έφτιαξε το LabLock δεν διαγράφονται.",
            "What failed stays in the record, so Restore this PC can try it again.", "Ό,τι απέτυχε μένει στο αρχείο, ώστε η Επαναφορά PC να το ξαναδοκιμάσει.",
            "The earlier computer name is used after a restart.", "Το προηγούμενο όνομα του PC ισχύει μετά από επανεκκίνηση.",
            "Accounts are no longer locked after wrong passwords.", "Οι λογαριασμοί δεν κλειδώνουν πια μετά από λάθος κωδικούς.",
            "LabLock has not renamed this PC, so its name stays as it is.", "Το LabLock δεν μετονόμασε αυτό το PC, γι' αυτό το όνομά του μένει όπως είναι.",
            "Nothing to restore: LabLock has not changed anything else on this PC.", "Τίποτα για επαναφορά: το LabLock δεν έχει αλλάξει τίποτε άλλο σε αυτό το PC.",

            // ---- groups of the checklist
            "SAFETY NET", "ΔΙΧΤΥ ΑΣΦΑΛΕΙΑΣ",
            "STUDENT ACCOUNT ONLY", "ΜΟΝΟ ΓΙΑ ΤΟΝ ΛΟΓΑΡΙΑΣΜΟ ΜΑΘΗΤΗ",
            "BROWSERS (CHROME, EDGE, BRAVE, FIREFOX) - ALL ACCOUNTS", "ΠΕΡΙΗΓΗΤΕΣ (CHROME, EDGE, BRAVE, FIREFOX) - ΟΛΟΙ ΟΙ ΛΟΓΑΡΙΑΣΜΟΙ",
            "WHOLE COMPUTER (ALL ACCOUNTS)", "ΟΛΟΣ Ο ΥΠΟΛΟΓΙΣΤΗΣ (ΟΛΟΙ ΟΙ ΛΟΓΑΡΙΑΣΜΟΙ)",
            "ALL ACCOUNTS", "ΟΛΟΙ ΟΙ ΛΟΓΑΡΙΑΣΜΟΙ",
            "ALL ACCOUNTS, STARTUP FOLDER", "ΟΛΟΙ ΟΙ ΛΟΓΑΡΙΑΣΜΟΙ, ΦΑΚΕΛΟΣ ΕΚΚΙΝΗΣΗΣ",
            "STUDENT ACCOUNT", "ΛΟΓΑΡΙΑΣΜΟΣ ΜΑΘΗΤΗ",
            "STUDENT ACCOUNT, STARTUP FOLDER", "ΛΟΓΑΡΙΑΣΜΟΣ ΜΑΘΗΤΗ, ΦΑΚΕΛΟΣ ΕΚΚΙΝΗΣΗΣ",

            // ---- states that are whole texts
            "Applied", "Εφαρμόστηκε",
            "Not applied", "Δεν εφαρμόστηκε",
            "Partly applied", "Εφαρμόστηκε εν μέρει",
            "Choose the student account", "Διάλεξε τον λογαριασμό μαθητή",
            "Applied (standard user)", "Εφαρμόστηκε (τυπικός χρήστης)",
            "Not applied (student is an administrator)", "Δεν εφαρμόστηκε (ο μαθητής είναι διαχειριστής)",
            "Applied (image forced)", "Εφαρμόστηκε (με επιβολή εικόνας)",
            "Applied (no image forced)", "Εφαρμόστηκε (χωρίς επιβολή εικόνας)",
            "Applied, but the list was edited - apply again", "Εφαρμόστηκε, αλλά η λίστα άλλαξε - εφάρμοσε ξανά",
            "Type an administrator name on the left", "Γράψε αριστερά ένα όνομα διαχειριστή",
            "Type a computer name on the left", "Γράψε αριστερά ένα όνομα υπολογιστή",
            "No connected network adapter", "Δεν υπάρχει συνδεδεμένη κάρτα δικτύου",
            "Another lock screen picture is forced", "Έχει επιβληθεί άλλη εικόνα οθόνης κλειδώματος",
            "Partly applied (locked, but with Windows' own picture)", "Εφαρμόστηκε εν μέρει (κλειδωμένη, αλλά με την εικόνα των Windows)",

            // ---- settings: titles and hints
            "Make a Windows restore point first", "Δημιουργία σημείου επαναφοράς των Windows πρώτα",
            "Turns on System Protection for the Windows drive and makes a restore point named LabLock before anything else is changed. Windows makes at most one per day. It is a general safety net; to take LabLock's settings back, use Undo.",
                "Ενεργοποιεί την Προστασία συστήματος στον δίσκο των Windows και δημιουργεί σημείο επαναφοράς με όνομα LabLock πριν αλλάξει οτιδήποτε άλλο. Τα Windows φτιάχνουν το πολύ ένα την ημέρα. Είναι γενικό δίχτυ ασφαλείας· για να πάρεις πίσω τις ρυθμίσεις του LabLock, χρησιμοποίησε την Αναίρεση.",
            "Create a separate administrator account", "Δημιουργία ξεχωριστού λογαριασμού διαχειριστή",
            "Creates the administrator account typed on the left with the password you type. If it already exists it is only checked; its password is not changed.",
                "Δημιουργεί τον λογαριασμό διαχειριστή που έγραψες αριστερά, με τον κωδικό που δίνεις. Αν υπάρχει ήδη, μόνο ελέγχεται· ο κωδικός του δεν αλλάζει.",
            "Make the student account a standard user (no admin rights)", "Ο λογαριασμός μαθητή γίνεται τυπικός χρήστης (χωρίς δικαιώματα διαχειριστή)",
            "The most important setting: without admin rights students cannot install programs or change the system. Only done after the administrator account above is confirmed to work.",
                "Η πιο σημαντική ρύθμιση: χωρίς δικαιώματα διαχειριστή οι μαθητές δεν μπορούν να εγκαταστήσουν προγράμματα ή να αλλάξουν το σύστημα. Γίνεται μόνο αφού επιβεβαιωθεί ότι ο παραπάνω λογαριασμός διαχειριστή δουλεύει.",
            "Student cannot change the account's password", "Ο μαθητής δεν μπορεί να αλλάξει τον κωδικό του λογαριασμού",
            "Stops a student from changing or setting the password of the shared account and locking everybody else out.",
                "Εμποδίζει έναν μαθητή να αλλάξει ή να βάλει κωδικό στον κοινό λογαριασμό και να κλειδώσει έξω όλους τους άλλους.",
            "Lock an account for 10 minutes after 10 wrong passwords", "Κλείδωμα λογαριασμού για 10 λεπτά μετά από 10 λάθος κωδικούς",
            "Stops students from guessing the administrator's password by trying again and again. It counts for every account, so somebody can also lock the student account for 10 minutes on purpose.",
                "Εμποδίζει τους μαθητές να μαντέψουν τον κωδικό του διαχειριστή δοκιμάζοντας ξανά και ξανά. Ισχύει για κάθε λογαριασμό, άρα κάποιος μπορεί και επίτηδες να κλειδώσει τον λογαριασμό μαθητή για 10 λεπτά.",
            "Lock the desktop wallpaper", "Κλείδωμα της ταπετσαρίας",
            "Forces one wallpaper for the student, so it cannot be changed from anywhere (not even with 'Set as desktop background'). Uses the image you chose, or the standard Windows wallpaper if you chose none.",
                "Επιβάλλει μία ταπετσαρία στον μαθητή, ώστε να μην αλλάζει από πουθενά (ούτε με το «Ορισμός ως φόντου επιφάνειας εργασίας»). Χρησιμοποιεί την εικόνα που διάλεξες ή, αν δεν διάλεξες, την τυπική ταπετσαρία των Windows.",
            "Block Settings and Control Panel completely", "Πλήρης αποκλεισμός Ρυθμίσεων και Πίνακα Ελέγχου",
            "The student cannot open the Settings app, any Settings page or Control Panel at all. They close again at once.",
                "Ο μαθητής δεν μπορεί να ανοίξει καθόλου τις Ρυθμίσεις, καμία σελίδα τους, ούτε τον Πίνακα Ελέγχου. Κλείνουν αμέσως.",
            "Lock the look of Windows (theme, colours, sounds, pointers, screen saver)", "Κλείδωμα της εμφάνισης των Windows (θέμα, χρώματα, ήχοι, δείκτες, προφύλαξη οθόνης)",
            "A second lock behind the blocked Settings: themes and .theme files, window colours, sounds, mouse pointers, desktop icons, screen saver and display options cannot be changed.",
                "Δεύτερη κλειδαριά πίσω από τις αποκλεισμένες Ρυθμίσεις: θέματα και αρχεία .theme, χρώματα παραθύρων, ήχοι, δείκτες ποντικιού, εικονίδια επιφάνειας εργασίας, προφύλαξη οθόνης και επιλογές οθόνης δεν αλλάζουν.",
            "Lock the taskbar and the Start menu", "Κλείδωμα της γραμμής εργασιών και του μενού Έναρξη",
            "The taskbar cannot be moved, resized or reconfigured, programs cannot be pinned or unpinned, and the Start menu layout cannot be changed.",
                "Η γραμμή εργασιών δεν μετακινείται, δεν αλλάζει μέγεθος ή ρυθμίσεις, δεν καρφιτσώνονται ούτε ξεκαρφιτσώνονται προγράμματα και η διάταξη του μενού Έναρξη δεν αλλάζει.",
            "Turn off the High Contrast keyboard shortcut", "Απενεργοποίηση της συντόμευσης Υψηλής Αντίθεσης",
            "Left Alt + Left Shift + Print Screen flips the whole screen to High Contrast colours even when Settings is blocked. Leave this off if a student needs High Contrast.",
                "Το αριστερό Alt + αριστερό Shift + Print Screen γυρίζει όλη την οθόνη σε χρώματα Υψηλής Αντίθεσης ακόμη κι όταν οι Ρυθμίσεις είναι αποκλεισμένες. Άφησέ το ατσεκάριστο αν κάποιος μαθητής χρειάζεται Υψηλή Αντίθεση.",
            "Block running programs from the student's Downloads and Desktop", "Αποκλεισμός εκτέλεσης προγραμμάτων από τις Λήψεις και την Επιφάνεια εργασίας του μαθητή",
            "A game or installer that was downloaded or copied there does not start. Documents and shortcuts still open. A determined student can move the file to another folder, so this stops the easy way, not every way.",
                "Ένα παιχνίδι ή πρόγραμμα εγκατάστασης που κατέβηκε ή αντιγράφηκε εκεί δεν ξεκινά. Τα έγγραφα και οι συντομεύσεις ανοίγουν κανονικά. Ένας επίμονος μαθητής μπορεί να μεταφέρει το αρχείο σε άλλον φάκελο, άρα κόβεται ο εύκολος δρόμος, όχι κάθε δρόμος.",
            "Empty the student's Downloads and Temp at every sign-in", "Άδειασμα των Λήψεων και των προσωρινών του μαθητή σε κάθε σύνδεση",
            "Everything in the student's Downloads and temporary files is DELETED each time somebody signs in to the PC. Documents, Desktop and Pictures are not touched. Tell the students to save their work in Documents or on a USB stick.",
                "Ό,τι υπάρχει στις Λήψεις και στα προσωρινά αρχεία του μαθητή ΔΙΑΓΡΑΦΕΤΑΙ κάθε φορά που κάποιος συνδέεται στο PC. Τα Έγγραφα, η Επιφάνεια εργασίας και οι Εικόνες δεν πειράζονται. Πες στους μαθητές να αποθηκεύουν τη δουλειά τους στα Έγγραφα ή σε USB.",
            "Block Registry Editor", "Αποκλεισμός του Επεξεργαστή Μητρώου",
            "The student cannot run regedit or import .reg files.", "Ο μαθητής δεν μπορεί να τρέξει το regedit ούτε να εισαγάγει αρχεία .reg.",
            "Block Task Manager", "Αποκλεισμός της Διαχείρισης Εργασιών",
            "The student cannot open Task Manager. Leave it off if students need it to close a frozen program.",
                "Ο μαθητής δεν μπορεί να ανοίξει τη Διαχείριση Εργασιών. Άφησέ το ατσεκάριστο αν οι μαθητές τη χρειάζονται για να κλείνουν ένα κολλημένο πρόγραμμα.",
            "Block Command Prompt", "Αποκλεισμός της Γραμμής Εντολών",
            "The student cannot open cmd.exe (batch files still run). Leave it off in a lab where students learn the command line.",
                "Ο μαθητής δεν μπορεί να ανοίξει το cmd.exe (τα αρχεία batch τρέχουν κανονικά). Άφησέ το ατσεκάριστο σε εργαστήριο όπου οι μαθητές μαθαίνουν γραμμή εντολών.",
            "Browsers must use the PC's DNS (so they cannot bypass Pi-hole)", "Οι περιηγητές χρησιμοποιούν το DNS του PC (για να μην παρακάμπτουν το Pi-hole)",
            "Turns off DNS-over-HTTPS. Without this a browser can ask another DNS server directly and skip the Pi-hole and the local blocklist.",
                "Απενεργοποιεί το DNS-over-HTTPS. Χωρίς αυτό ένας περιηγητής μπορεί να ρωτήσει απευθείας άλλον διακομιστή DNS και να προσπεράσει το Pi-hole και την τοπική λίστα αποκλεισμού.",
            "Browsers forget everything when they are closed", "Οι περιηγητές ξεχνούν τα πάντα όταν κλείνουν",
            "History, cookies and sign-ins are thrown away when the browser closes, so a student never finds the previous student's e-mail or social accounts open. Bookmarks made by students are lost too.",
                "Ιστορικό, cookies και συνδέσεις σβήνονται όταν κλείνει ο περιηγητής, ώστε ένας μαθητής να μη βρίσκει ποτέ ανοιχτό το e-mail ή τα κοινωνικά δίκτυα του προηγούμενου. Χάνονται και οι σελιδοδείκτες που φτιάχνουν οι μαθητές.",
            "Browsers do not save passwords or form data", "Οι περιηγητές δεν αποθηκεύουν κωδικούς ή στοιχεία φορμών",
            "The browser never offers to remember a password, an address or a card. On a shared account a saved password belongs to whoever sits down next.",
                "Ο περιηγητής δεν προτείνει ποτέ να θυμάται κωδικό, διεύθυνση ή κάρτα. Σε κοινό λογαριασμό, ένας αποθηκευμένος κωδικός ανήκει σε όποιον καθίσει μετά.",
            "No private windows, guest mode, extra profiles or browser sign-in", "Όχι ιδιωτικά παράθυρα, λειτουργία επισκέπτη, επιπλέον προφίλ ή σύνδεση του περιηγητή",
            "Students cannot open a private or guest window, add a browser profile or sign the browser itself in to an account. Signing in to web sites (e-mail, e-class) still works.",
                "Οι μαθητές δεν μπορούν να ανοίξουν ιδιωτικό παράθυρο ή παράθυρο επισκέπτη, να προσθέσουν προφίλ ή να συνδέσουν τον ίδιο τον περιηγητή σε λογαριασμό. Η σύνδεση σε ιστοσελίδες (e-mail, e-class) δουλεύει κανονικά.",
            "No browser extensions", "Όχι επεκτάσεις στους περιηγητές",
            "Extensions cannot be installed and the ones already there are switched off. This also stops VPN and proxy extensions, the usual way around a filter. Leave it off if the lab depends on an extension.",
                "Δεν εγκαθίστανται επεκτάσεις και όσες υπάρχουν ήδη απενεργοποιούνται. Έτσι κόβονται και οι επεκτάσεις VPN και proxy, ο συνηθισμένος τρόπος παράκαμψης ενός φίλτρου. Άφησέ το ατσεκάριστο αν το εργαστήριο χρειάζεται κάποια επέκταση.",
            "Safe search on Google and Bing, restricted mode on YouTube", "Ασφαλής αναζήτηση σε Google και Bing, περιορισμένη λειτουργία στο YouTube",
            "Forces SafeSearch on Google and Bing and the moderate restricted mode on YouTube in Chrome, Edge and Brave. Firefox has no such policy.",
                "Επιβάλλει το SafeSearch σε Google και Bing και τη μέτρια περιορισμένη λειτουργία στο YouTube, σε Chrome, Edge και Brave. Ο Firefox δεν έχει τέτοια πολιτική.",
            "Block the game sites in the blocklist", "Αποκλεισμός των ιστότοπων παιχνιδιών της λίστας",
            "Use the school image as the picture of every account, and lock it", "Η εικόνα του σχολείου ως εικόνα κάθε λογαριασμού, κλειδωμένη",
            "Replaces Windows' standard account picture with the school image and makes every account use it, so nobody can put another picture there. The original pictures are kept and come back with Undo.",
                "Αντικαθιστά την τυπική εικόνα λογαριασμού των Windows με την εικόνα του σχολείου και την επιβάλλει σε κάθε λογαριασμό, ώστε κανείς να μη βάζει άλλη εικόνα. Οι αρχικές εικόνες φυλάσσονται και επιστρέφουν με την Αναίρεση.",
            "Use the school image on the lock screen, and lock it", "Η εικόνα του σχολείου στην οθόνη κλειδώματος, κλειδωμένη",
            "Makes a lock screen picture from the school image (a logo is centred on white, a wide photo fills the screen) and stops everybody from choosing another one.",
                "Φτιάχνει εικόνα οθόνης κλειδώματος από την εικόνα του σχολείου (ένα λογότυπο μπαίνει στο κέντρο σε λευκό φόντο, μια πλατιά φωτογραφία γεμίζει την οθόνη) και δεν αφήνει κανέναν να διαλέξει άλλη.",
            "Block running programs from USB sticks (all accounts)", "Αποκλεισμός εκτέλεσης προγραμμάτων από USB (όλοι οι λογαριασμοί)",
            "Programs on a USB stick or USB disk do not start; files on it still open. It counts for every account, so afterwards copy LabLock.exe to the PC's disk to run it. Takes effect when the stick is plugged in again.",
                "Τα προγράμματα σε USB stick ή δίσκο USB δεν ξεκινούν· τα αρχεία του ανοίγουν κανονικά. Ισχύει για κάθε λογαριασμό, γι' αυτό μετά αντίγραψε το LabLock.exe στον δίσκο του PC για να το τρέξεις. Ισχύει όταν το USB ξανασυνδεθεί.",
            "Give this PC the fixed IP address typed on the left", "Σταθερή διεύθυνση IP στο PC, αυτή που έγραψες αριστερά",
            "Sets the IP address, subnet mask, gateway and DNS servers on the PC's network adapter, after checking that no other device answers on that address. Undo puts back the address the PC had before LabLock (or an automatic address, if it had one).",
                "Ορίζει διεύθυνση IP, μάσκα υποδικτύου, πύλη και διακομιστές DNS στην κάρτα δικτύου του PC, αφού ελέγξει ότι καμία άλλη συσκευή δεν απαντά σε αυτή τη διεύθυνση. Η Αναίρεση βάζει πίσω τη διεύθυνση που είχε το PC πριν από το LabLock (ή αυτόματη, αν είχε αυτόματη).",
            "Use the DNS servers typed on the left (Pi-hole), keep the IP address as it is", "Χρήση των διακομιστών DNS που έγραψες αριστερά (Pi-hole), χωρίς αλλαγή της IP",
            "Only changes the DNS servers of the connected network adapters. Not needed together with the fixed IP setting, which sets the DNS too. A second DNS server that is not the Pi-hole lets blocked sites through.",
                "Αλλάζει μόνο τους διακομιστές DNS στις συνδεδεμένες κάρτες δικτύου. Δεν χρειάζεται μαζί με τη ρύθμιση της σταθερής IP, που ορίζει και το DNS. Ένας δεύτερος διακομιστής DNS που δεν είναι το Pi-hole αφήνει να περνούν αποκλεισμένοι ιστότοποι.",
            "Allow DNS only through the DNS servers typed on the left (firewall)", "DNS μόνο μέσω των διακομιστών που έγραψες αριστερά (τείχος προστασίας)",
            "Firewall rules block DNS to every other server, so the Pi-hole cannot be bypassed with another DNS. The PC must already use the typed servers. If you type a second server, it stays allowed as the spare for when the Pi-hole is down.",
                "Κανόνες του τείχους προστασίας κόβουν το DNS προς κάθε άλλον διακομιστή, ώστε το Pi-hole να μην παρακάμπτεται με άλλο DNS. Το PC πρέπει να χρησιμοποιεί ήδη τους διακομιστές που έγραψες. Αν γράψεις και δεύτερο διακομιστή, μένει επιτρεπτός ως εφεδρικός για όταν πέσει το Pi-hole.",
            "Turn off Microsoft Store (Education / Enterprise editions only)", "Απενεργοποίηση του Microsoft Store (μόνο σε εκδόσεις Education / Enterprise)",
            "Windows only obeys this policy on Education and Enterprise. On Home and Pro it is written but has no effect.",
                "Τα Windows υπακούν σε αυτή την πολιτική μόνο σε Education και Enterprise. Σε Home και Pro γράφεται αλλά δεν έχει αποτέλεσμα.",
            "Allow remote management from the teacher PC (PowerShell remoting)", "Απομακρυσμένη διαχείριση από το PC του καθηγητή (PowerShell remoting)",
            "Turns on PowerShell remoting so that later you can send commands to this PC with Invoke-Command, using the administrator account. Leave it off if you do not need that.",
                "Ενεργοποιεί το PowerShell remoting ώστε αργότερα να στέλνεις εντολές σε αυτό το PC με Invoke-Command, με τον λογαριασμό διαχειριστή. Άφησέ το ατσεκάριστο αν δεν το χρειάζεσαι.",
            "Rename this PC to the name typed on the left (done last, then restart)", "Μετονομασία του PC στο όνομα που έγραψες αριστερά (γίνεται τελευταία, μετά επανεκκίνηση)",
            "Changes the computer name. It runs after all other settings and LabLock then offers to restart the PC, because Windows only uses the new name after a restart.",
                "Αλλάζει το όνομα του υπολογιστή. Τρέχει μετά από όλες τις άλλες ρυθμίσεις και μετά το LabLock προτείνει επανεκκίνηση, γιατί τα Windows χρησιμοποιούν το νέο όνομα μόνο μετά από επανεκκίνηση.",

            // ---- messages of the settings
            "Type a password for the new administrator account.", "Γράψε κωδικό για τον νέο λογαριασμό διαχειριστή.",
            "The administrator account must not be the student account.", "Ο λογαριασμός διαχειριστή δεν μπορεί να είναι ο λογαριασμός μαθητή.",
            "The student account was not found.", "Ο λογαριασμός μαθητή δεν βρέθηκε.",
            "No student account selected.", "Δεν έχει επιλεγεί λογαριασμός μαθητή.",
            "No profile yet - sign in once with this account, then run LabLock again.", "Δεν υπάρχει ακόμη προφίλ - συνδέσου μία φορά με αυτόν τον λογαριασμό και τρέξε ξανά το LabLock.",
            "A previous run left the student's settings open. Restart this PC and try again.", "Μια προηγούμενη εκτέλεση άφησε ανοιχτές τις ρυθμίσεις του μαθητή. Κάνε επανεκκίνηση και δοκίμασε ξανά.",
            "The student's settings are not available.", "Οι ρυθμίσεις του μαθητή δεν είναι διαθέσιμες.",
            "The student's settings could not be opened.", "Οι ρυθμίσεις του μαθητή δεν μπόρεσαν να ανοίξουν.",
            "WARNING: the student's settings file could not be closed. Restart this PC before the student signs in.",
                "ΠΡΟΣΟΧΗ: το αρχείο ρυθμίσεων του μαθητή δεν μπόρεσε να κλείσει. Κάνε επανεκκίνηση του PC πριν συνδεθεί ο μαθητής.",
            "The student's profile folder was not found. Sign in once with that account first.", "Ο φάκελος προφίλ του μαθητή δεν βρέθηκε. Συνδέσου πρώτα μία φορά με αυτόν τον λογαριασμό.",
            "No folder of the student could be cleaned safely.", "Κανένας φάκελος του μαθητή δεν μπορεί να καθαριστεί με ασφάλεια.",
            "Could not create the clean-up task", "Δεν δημιουργήθηκε η εργασία καθαρισμού",
            "The student's Downloads and Temp are emptied at every sign-in from now on.", "Από εδώ και πέρα οι Λήψεις και τα προσωρινά του μαθητή αδειάζουν σε κάθε σύνδεση.",
            "Could not add the firewall rule", "Δεν προστέθηκε ο κανόνας του τείχους προστασίας",
            "Could not set the lockout rule", "Δεν ορίστηκε ο κανόνας κλειδώματος",
            "Could not remove the lockout rule", "Δεν αφαιρέθηκε ο κανόνας κλειδώματος",
            "Making a restore point. This can take a minute...", "Δημιουργείται σημείο επαναφοράς. Μπορεί να πάρει ένα λεπτό...",
            "Windows could not make a restore point", "Τα Windows δεν μπόρεσαν να δημιουργήσουν σημείο επαναφοράς",
            "Restore points stay on the PC. Windows removes old ones by itself.", "Τα σημεία επαναφοράς μένουν στο PC. Τα Windows σβήνουν μόνα τους τα παλιά.",
            "The standard Windows wallpaper was not found, so no image is forced. Choose a wallpaper image to make the lock complete.",
                "Η τυπική ταπετσαρία των Windows δεν βρέθηκε, άρα δεν επιβάλλεται εικόνα. Διάλεξε εικόνα ταπετσαρίας για να είναι πλήρες το κλείδωμα.",
            "No wallpaper image chosen: the standard Windows wallpaper is forced.", "Δεν διάλεξες εικόνα ταπετσαρίας: επιβάλλεται η τυπική ταπετσαρία των Windows.",
            "The blocklist was removed from the browsers and the hosts file.", "Η λίστα αποκλεισμού αφαιρέθηκε από τους περιηγητές και το αρχείο hosts.",
            "Could not change the password rule", "Δεν άλλαξε ο κανόνας του κωδικού",
            "Could not turn on PowerShell remoting", "Δεν ενεργοποιήθηκε το PowerShell remoting",
            "Could not turn off PowerShell remoting", "Δεν απενεργοποιήθηκε το PowerShell remoting",
            "LabLock has not renamed this PC, so there is no earlier name to go back to.", "Το LabLock δεν έχει μετονομάσει αυτό το PC, άρα δεν υπάρχει προηγούμενο όνομα για επιστροφή.",
            "Type a computer name.", "Γράψε ένα όνομα υπολογιστή.",
            "A computer name can be at most 15 characters long.", "Το όνομα υπολογιστή έχει το πολύ 15 χαρακτήρες.",
            "A computer name may only contain the letters A-Z, digits and the hyphen (-), without spaces.", "Το όνομα υπολογιστή περιέχει μόνο λατινικά γράμματα A-Z, ψηφία και παύλα (-), χωρίς κενά.",
            "A computer name cannot consist of digits only.", "Το όνομα υπολογιστή δεν μπορεί να έχει μόνο ψηφία.",
            "A computer name cannot start or end with a hyphen.", "Το όνομα υπολογιστή δεν μπορεί να αρχίζει ή να τελειώνει με παύλα.",
            "This PC is joined to a domain. Rename it from the domain, not with LabLock.", "Αυτό το PC ανήκει σε domain. Μετονόμασέ το από το domain, όχι με το LabLock.",
            "Type the IP address for this PC, for example 192.168.2.21.", "Γράψε τη διεύθυνση IP αυτού του PC, π.χ. 192.168.2.21.",
            "Type the subnet mask, for example 255.255.255.0.", "Γράψε τη μάσκα υποδικτύου, π.χ. 255.255.255.0.",
            "The gateway is not a valid IP address (leave it empty if there is none).", "Η πύλη δεν είναι έγκυρη διεύθυνση IP (άφησέ την κενή αν δεν υπάρχει).",
            "The PC and the gateway cannot have the same IP address.", "Το PC και η πύλη δεν μπορούν να έχουν την ίδια διεύθυνση IP.",
            "Type the IP address of the DNS server, for example 192.168.2.48.", "Γράψε τη διεύθυνση IP του διακομιστή DNS, π.χ. 192.168.2.48.",
            "The second DNS server is not a valid IP address (leave it empty if there is none).", "Ο δεύτερος διακομιστής DNS δεν είναι έγκυρη διεύθυνση IP (άφησέ τον κενό αν δεν υπάρχει).",
            "Type the IP address of the DNS server first.", "Γράψε πρώτα τη διεύθυνση IP του διακομιστή DNS.",
            "No connected network adapter was found.", "Δεν βρέθηκε συνδεδεμένη κάρτα δικτύου.",
            "A PC with a fixed address needs a DNS server. Type one first.", "Ένα PC με σταθερή διεύθυνση χρειάζεται διακομιστή DNS. Γράψε πρώτα έναν.",
            "No connected network adapter was found. Plug in the network cable first.", "Δεν βρέθηκε συνδεδεμένη κάρτα δικτύου. Σύνδεσε πρώτα το καλώδιο δικτύου.",

            // ---- This PC page
            "WHAT THIS PC IS MADE OF", "ΑΠΟ ΤΙ ΑΠΟΤΕΛΕΙΤΑΙ ΑΥΤΟ ΤΟ PC",
            "Manufacturer", "Κατασκευαστής",
            "Model", "Μοντέλο",
            "Serial number", "Σειριακός αρ.",
            "Processor", "Επεξεργαστής",
            "Memory", "Μνήμη",
            "Disk", "Δίσκος",
            "MAC address", "Διεύθυνση MAC",
            "THE RECORD OF THE LAB'S PCS", "ΤΟ ΑΡΧΕΙΟ ΤΩΝ PC ΤΟΥ ΕΡΓΑΣΤΗΡΙΟΥ",
            "Room / lab", "Αίθουσα",
            "Notes for this PC", "Σημειώσεις",
            " PCs are in the record so far.", " PC υπάρχουν στο αρχείο μέχρι τώρα.",
            "Save this PC to the record", "Αποθήκευση του PC στο αρχείο",
            "Open the record", "Άνοιγμα του αρχείου",
            "Export for Veyon", "Εξαγωγή για Veyon",
            "Export for NetHawk", "Εξαγωγή για NetHawk",
            "The record is the file LabLock-computers.csv next to LabLock.exe: one line per PC, kept up to date after every Apply. A PC is recognised by its MAC address, so a new name or IP address updates its line. The name, IP address and DNS of this PC are set on the Lockdown page.",
                "Το αρχείο είναι το LabLock-computers.csv δίπλα στο LabLock.exe: μία γραμμή για κάθε PC, που ενημερώνεται μετά από κάθε Εφαρμογή. Ένα PC αναγνωρίζεται από τη διεύθυνση MAC, άρα ένα νέο όνομα ή μια νέα IP ενημερώνει τη γραμμή του. Το όνομα, η IP και το DNS αυτού του PC ορίζονται στη σελίδα Κλείδωμα.",
            "The record of PCs could not be written. If the file is open in Excel, close it and try again.", "Το αρχείο των PC δεν μπόρεσε να γραφτεί. Αν είναι ανοιχτό στο Excel, κλείσ' το και δοκίμασε ξανά.",
            "This PC is in the record of PCs.", "Αυτό το PC γράφτηκε στο αρχείο των PC.",
            "Written: ", "Γράφτηκε: ",
            "On the teacher's PC, in a command prompt opened as administrator, run:", "Στο PC του καθηγητή, σε γραμμή εντολών ανοιγμένη ως διαχειριστής, τρέξε:",
            "It has the format of NetHawk's machines.json. With NetHawk closed, copy its entries into the file machines.json in NetHawk's data folder.",
                "Έχει τη μορφή του machines.json του NetHawk. Με το NetHawk κλειστό, αντίγραψε τις εγγραφές του στο αρχείο machines.json στον φάκελο δεδομένων του NetHawk.",

            // ---- Startup page
            "These programs start by themselves when somebody signs in. Untick the ones that should not start and press Save changes. Nothing is deleted or uninstalled: tick a line again to bring it back. It is the same switch Task Manager shows.",
                "Αυτά τα προγράμματα ξεκινούν μόνα τους όταν κάποιος συνδέεται. Ξετσέκαρε όσα δεν πρέπει να ξεκινούν και πάτησε Αποθήκευση αλλαγών. Τίποτα δεν διαγράφεται ούτε απεγκαθίσταται: τσέκαρε ξανά μια γραμμή για να επανέλθει. Είναι ο ίδιος διακόπτης που δείχνει η Διαχείριση Εργασιών.",
            "Save changes", "Αποθήκευση αλλαγών",
            " startup programs found.", " προγράμματα εκκίνησης βρέθηκαν.",
            "Choose the student account on the Lockdown page to see the student's own ones too.", "Διάλεξε τον λογαριασμό μαθητή στη σελίδα Κλείδωμα για να δεις και τα δικά του.",
            "The startup programs could not be read: ", "Τα προγράμματα εκκίνησης δεν μπόρεσαν να διαβαστούν: ",
            "Nothing was changed. Tick or untick a program first.", "Δεν άλλαξε κάτι. Τσέκαρε ή ξετσέκαρε πρώτα ένα πρόγραμμα.",
            " changed, ", " άλλαξαν, ",
            "The change counts from the next sign-in.", "Η αλλαγή ισχύει από την επόμενη σύνδεση.",

            // ---- Health check page
            "A look at the things that usually go wrong on a lab PC. It only reads: nothing is changed. Orange lines deserve a look, green ones are fine.",
                "Μια ματιά σε όσα συνήθως πάνε στραβά σε ένα PC εργαστηρίου. Μόνο διαβάζει: τίποτα δεν αλλάζει. Οι πορτοκαλί γραμμές θέλουν προσοχή, οι πράσινες είναι εντάξει.",
            "Run the check", "Εκτέλεση ελέγχου",
            "Save the report", "Αποθήκευση αναφοράς",
            "Press Run the check. It takes a few seconds.", "Πάτησε Εκτέλεση ελέγχου. Παίρνει λίγα δευτερόλεπτα.",
            "Checking...", "Γίνεται έλεγχος...",
            "Nothing needs attention.", "Τίποτα δεν θέλει προσοχή.",
            " lines deserve a look.", " γραμμές θέλουν προσοχή.",
            "The check could not be done: ", "Ο έλεγχος δεν μπόρεσε να γίνει: ",
            "Run the check first.", "Τρέξε πρώτα τον έλεγχο.",
            "A check could not be done", "Ένας έλεγχος δεν μπόρεσε να γίνει",
            "Administrator accounts", "Λογαριασμοί διαχειριστή",
            "No enabled administrator account was found.", "Δεν βρέθηκε ενεργός λογαριασμός διαχειριστή.",
            "Windows' built-in Administrator and Guest accounts", "Οι ενσωματωμένοι λογαριασμοί Administrator και Guest των Windows",
            "Both are switched off.", "Είναι και οι δύο απενεργοποιημένοι.",
            "The student account", "Ο λογαριασμός μαθητή",
            "No student account is chosen on the Lockdown page.", "Δεν έχει επιλεγεί λογαριασμός μαθητή στη σελίδα Κλείδωμα.",
            "Automatic sign-in", "Αυτόματη σύνδεση",
            "A password is stored in the registry in readable form for automatic sign-in.", "Στο μητρώο είναι αποθηκευμένος κωδικός σε αναγνώσιμη μορφή για την αυτόματη σύνδεση.",
            "Off.", "Ανενεργό.",
            "On.", "Ενεργό.",
            "None.", "Κανένα.",
            "User Account Control (the administrator prompt)", "Έλεγχος λογαριασμού χρήστη (το παράθυρο διαχειριστή)",
            "Off. Programs get administrator rights without asking.", "Ανενεργός. Τα προγράμματα παίρνουν δικαιώματα διαχειριστή χωρίς ερώτηση.",
            "Microsoft Defender antivirus", "Αντιιικό Microsoft Defender",
            "Could not be read. Another antivirus program may be installed.", "Δεν διαβάστηκε. Ίσως είναι εγκατεστημένο άλλο αντιιικό.",
            "Antivirus exclusions", "Εξαιρέσεις του αντιιικού",
            "Windows Firewall", "Τείχος προστασίας των Windows",
            "On for all network types.", "Ενεργό για όλους τους τύπους δικτύου.",
            "Windows updates", "Ενημερώσεις των Windows",
            "The date of the last update could not be read.", "Η ημερομηνία της τελευταίας ενημέρωσης δεν διαβάστηκε.",
            "Restart", "Επανεκκίνηση",
            "Windows is waiting for a restart to finish updates or a new computer name.", "Τα Windows περιμένουν επανεκκίνηση για να ολοκληρωθούν ενημερώσεις ή νέο όνομα υπολογιστή.",
            "No restart is waiting.", "Δεν εκκρεμεί επανεκκίνηση.",
            "Free disk space", "Ελεύθερος χώρος στον δίσκο",
            "Programs installed inside the student's account", "Προγράμματα εγκατεστημένα μέσα στον λογαριασμό μαθητή",
            "Program files in the student's Downloads and Desktop", "Αρχεία προγραμμάτων στις Λήψεις και την Επιφάνεια εργασίας του μαθητή",
            "Programs that start with Windows", "Προγράμματα που ξεκινούν με τα Windows",
            "The hosts file", "Το αρχείο hosts",
            "Only Windows' own lines and LabLock's blocklist.", "Μόνο οι γραμμές των Windows και η λίστα αποκλεισμού του LabLock.",
            "DNS servers in use", "Διακομιστές DNS σε χρήση",
            "None found (no network?).", "Δεν βρέθηκαν (χωρίς δίκτυο;).",
            "Veyon service", "Υπηρεσία Veyon",
            "Running.", "Εκτελείται.",
            "Installed but not running, so this PC cannot be seen from Veyon Master.", "Εγκατεστημένη αλλά δεν εκτελείται, άρα το PC δεν φαίνεται από το Veyon Master.",
            "The report could not be saved: ", "Η αναφορά δεν μπόρεσε να αποθηκευτεί: ",
            "The file could not be opened: ", "Το αρχείο δεν μπόρεσε να ανοίξει: ",

            // ---- About page
            "Version ", "Έκδοση ",
            "CREATED BY", "ΔΗΜΙΟΥΡΓΟΣ",
            "SUPPORT THE PROJECT", "ΣΤΗΡΙΞΕ ΤΟ ΕΡΓΟ",
            "GitHub profile", "Προφίλ στο GitHub",
            "Open the LabLock folder", "Άνοιγμα του φακέλου του LabLock",
            "The school image (change it on the Lockdown page)", "Η εικόνα του σχολείου (αλλάζει στη σελίδα Κλείδωμα)",

            // ---- silent run
            "Silent run refused: LabLock was not started as administrator.", "Η αθόρυβη εκτέλεση απορρίφθηκε: το LabLock δεν ξεκίνησε ως διαχειριστής.",
            "Nothing to do: LabLock.ini has no ticked settings. Open LabLock once, tick what you want and close it.",
                "Δεν υπάρχει κάτι να γίνει: το LabLock.ini δεν έχει τσεκαρισμένες ρυθμίσεις. Άνοιξε μία φορά το LabLock, τσέκαρε ό,τι θέλεις και κλείσ' το."
        };

        /// <summary>Pieces of texts that are put together while the program runs.</summary>
        public static readonly string[] Parts =
        {
            // the log
            "OK      ", "OK      ",
            "UNDONE  ", "ΑΝΑΙΡΕΘΗΚΕ  ",
            "FAILED  ", "ΑΠΕΤΥΧΕ  ",
            "ERROR  ", "ΣΦΑΛΜΑ  ",
            "Applying on ", "Εφαρμογή στο ",
            "Undoing on ", "Αναίρεση στο ",
            " (silent run) ----", " (αθόρυβη εκτέλεση) ----",
            " done, ", " έγιναν, ",
            " failed.", " απέτυχαν.",
            "Cannot check: ", "Αδύνατος ο έλεγχος: ",
            "Restoring ", "Επαναφορά: ",
            "School Network Manager did not answer at ", "Το School Network Manager δεν απάντησε στο ",
            "WARNING: School Network Manager did not take the report: ", "ΠΡΟΣΟΧΗ: το School Network Manager δεν δέχτηκε την αναφορά: ",
            "the key (snmkey) is wrong", "το κλειδί (snmkey) είναι λάθος",
            "Now: ", "Τώρα: ",
            "Shut this board's Windows down every day at ", "Κλείσιμο των Windows του πίνακα κάθε μέρα στις ",
            "Applied (every day at ", "Εφαρμόστηκε (κάθε μέρα στις ",
            "This PC shuts down every night at ", "Το PC κλείνει κάθε βράδυ στις ",
            " if it is still on (warning at ", " αν είναι ακόμη ανοιχτό (προειδοποίηση στις ",
            "Wake-up signal sent to ", "Στάλθηκε σήμα αφύπνισης σε ",
            " PCs.", " PC.",
            "Not a usable MAC address, skipped: ", "Μη έγκυρη διεύθυνση MAC, παραλείφθηκε: ",
            " (Fast Startup is on)", " (η Γρήγορη εκκίνηση είναι ενεργή)",
            " (the card may not wake the PC)", " (η κάρτα δεν επιτρέπεται να ξυπνά το PC)",
            "WARNING: Windows did not allow '", "ΠΡΟΣΟΧΗ: τα Windows δεν επέτρεψαν στο '",
            "' to wake the PC. In Device Manager > the network card > Power Management, tick \"Allow this device to wake the computer\".",
                "' να ξυπνά το PC. Στη Διαχείριση Συσκευών > κάρτα δικτύου > Διαχείριση ενέργειας, τσέκαρε «Να επιτρέπεται σε αυτήν τη συσκευή να αφυπνίζει τον υπολογιστή».",
            "Wake-on-LAN is ready on '", "Το Wake-on-LAN είναι έτοιμο στο '",
            "). It works after the next restart, when the PC is shut down normally and the network cable stays in.",
                "). Λειτουργεί μετά την επόμενη επανεκκίνηση, όταν το PC κλείνει κανονικά και το καλώδιο δικτύου μένει συνδεδεμένο.",
            " to how it was before LabLock ----", " όπως ήταν πριν από το LabLock ----",
            "' was not an administrator before LabLock, so it stays a standard user.", "' δεν ήταν διαχειριστής πριν από το LabLock, γι' αυτό μένει τυπικός χρήστης.",
            "WARNING: the record of changes on this PC could not be updated: ", "ΠΡΟΣΟΧΗ: το αρχείο αλλαγών του PC δεν ενημερώθηκε: ",
            "The lockout rule is back to ", "Ο κανόνας κλειδώματος γύρισε στους ",
            " wrong passwords, as before LabLock.", " λάθος κωδικούς, όπως πριν από το LabLock.",
            "' has its earlier fixed address ", "' έχει ξανά την προηγούμενη σταθερή διεύθυνση ",
            " again (as before LabLock).", " (όπως πριν από το LabLock).",
            "Could not put the earlier address back on '", "Δεν μπήκε πίσω η προηγούμενη διεύθυνση στο '",
            "Left as it is in a silent run (restore it at the PC itself): ", "Έμεινε όπως είναι στην αθόρυβη εκτέλεση (κάνε επαναφορά στο ίδιο το PC): ",

            // accounts in the list
            "  (shown as \"", "  (εμφανίζεται ως \"",
            "  -  administrator", "  -  διαχειριστής",
            "  -  standard user", "  -  τυπικός χρήστης",
            ", disabled", ", απενεργοποιημένος",

            // states with a name or a number inside
            "Applied (", "Εφαρμόστηκε (",
            "Not applied (", "Δεν εφαρμόστηκε (",
            "' does not exist)", "' δεν υπάρχει)",
            "' exists but is disabled", "' υπάρχει αλλά είναι απενεργοποιημένος",
            "' is an administrator)", "' είναι διαχειριστής)",
            "' exists, not an administrator", "' υπάρχει, δεν είναι διαχειριστής",
            "Applied (after ", "Εφαρμόστηκε (μετά από ",
            " wrong passwords)", " λάθος κωδικούς)",
            "Applied (restore point of ", "Εφαρμόστηκε (σημείο επαναφοράς της ",
            "Not applied (DNS now: ", "Δεν εφαρμόστηκε (DNS τώρα: ",
            "Not applied (automatic, now ", "Δεν εφαρμόστηκε (αυτόματη, τώρα ",
            "no address", "χωρίς διεύθυνση",
            "Applied (fixed at ", "Εφαρμόστηκε (σταθερή στο ",
            "Fixed at ", "Σταθερή στο ",
            " now, not the address typed", " τώρα, όχι η διεύθυνση που έγραψες",
            " entries)", " εγγραφές)",
            " after the restart, still ", " μετά την επανεκκίνηση, ακόμη ",
            "Restart waiting: ", "Εκκρεμεί επανεκκίνηση: ",
            "Applied (this PC is called ", "Εφαρμόστηκε (το PC λέγεται ",
            "Not applied (this PC is called ", "Δεν εφαρμόστηκε (το PC λέγεται ",

            // messages with a name inside
            "Could not read the network settings of this PC: ", "Δεν διαβάστηκαν οι ρυθμίσεις δικτύου του PC: ",
            "Could not read the accounts of this PC: ", "Δεν διαβάστηκαν οι λογαριασμοί του PC: ",
            "Could not read the current state: ", "Δεν διαβάστηκε η τρέχουσα κατάσταση: ",
            "The record of PCs (", "Το αρχείο των PC (",
            ") could not be updated: ", ") δεν μπόρεσε να ενημερωθεί: ",
            "' is not a working administrator account on this PC.", "' δεν είναι λειτουργικός λογαριασμός διαχειριστή σε αυτό το PC.",
            "' is not a working administrator account on this PC, so the student account was NOT changed. Tick 'Create a separate administrator account' too.",
                "' δεν είναι λειτουργικός λογαριασμός διαχειριστή σε αυτό το PC, γι' αυτό ο λογαριασμός μαθητή ΔΕΝ άλλαξε. Τσέκαρε και το «Δημιουργία ξεχωριστού λογαριασμού διαχειριστή».",
            "The wallpaper image was not found:", "Η εικόνα της ταπετσαρίας δεν βρέθηκε:",
            "Choose a school image first, or put ", "Διάλεξε πρώτα εικόνα σχολείου ή βάλε το ",
            " next to LabLock.exe.", " δίπλα στο LabLock.exe.",
            "Created the standard account '", "Δημιουργήθηκε ο τυπικός λογαριασμός '",
            "'. Sign in with it once, so Windows makes its profile, then run LabLock again.", "'. Συνδέσου μία φορά με αυτόν, για να φτιάξουν τα Windows το προφίλ του, και τρέξε ξανά το LabLock.",
            "Notepad could not be started: ", "Το Σημειωματάριο δεν ξεκίνησε: ",
            "Created the account '", "Δημιουργήθηκε ο λογαριασμός '",
            "The account '", "Ο λογαριασμός '",
            "' exists but is disabled. Enable it or choose another name.", "' υπάρχει αλλά είναι απενεργοποιημένος. Ενεργοποίησέ τον ή διάλεξε άλλο όνομα.",
            "' already exists; its password was left unchanged.", "' υπάρχει ήδη· ο κωδικός του έμεινε όπως ήταν.",
            "Could not confirm that '", "Δεν επιβεβαιώθηκε ότι ο '",
            "' is an administrator.", "' είναι διαχειριστής.",
            "LabLock never deletes accounts. To remove '", "Το LabLock δεν διαγράφει ποτέ λογαριασμούς. Για να αφαιρέσεις τον '",
            "', use Computer Management > Local Users and Groups.", "', χρησιμοποίησε τη Διαχείριση Υπολογιστή > Τοπικοί χρήστες και ομάδες.",
            "' is now a standard user. This takes effect the next time the student signs in.", "' είναι πλέον τυπικός χρήστης. Ισχύει από την επόμενη σύνδεση του μαθητή.",
            "' is an administrator again (from the next sign-in).", "' είναι ξανά διαχειριστής (από την επόμενη σύνδεση).",
            "Could not create the account '", "Δεν δημιουργήθηκε ο λογαριασμός '",
            "Could not add the account to the group: ", "Ο λογαριασμός δεν προστέθηκε στην ομάδα: ",
            "Could not remove the account from the group: ", "Ο λογαριασμός δεν αφαιρέθηκε από την ομάδα: ",
            "Could not read the members of the group: ", "Δεν διαβάστηκαν τα μέλη της ομάδας: ",
            "Could not read the lockout rule: ", "Δεν διαβάστηκε ο κανόνας κλειδώματος: ",
            "access denied", "άρνηση πρόσβασης",
            "this is a built-in account and cannot be changed this way", "είναι ενσωματωμένος λογαριασμός και δεν αλλάζει με αυτόν τον τρόπο",
            "the account name is not valid", "το όνομα λογαριασμού δεν είναι έγκυρο",
            "the group was not found", "η ομάδα δεν βρέθηκε",
            "the account was not found", "ο λογαριασμός δεν βρέθηκε",
            "the account already exists", "ο λογαριασμός υπάρχει ήδη",
            "the password does not meet this PC's password rules (too short or too simple)", "ο κωδικός δεν πληροί τους κανόνες κωδικών αυτού του PC (πολύ μικρός ή πολύ απλός)",
            "Wallpaper image copied to ", "Η εικόνα ταπετσαρίας αντιγράφηκε στο ",
            "The wallpaper image was not found: ", "Η εικόνα της ταπετσαρίας δεν βρέθηκε: ",
            "Folder not found, skipped: ", "Ο φάκελος δεν βρέθηκε και παραλείφθηκε: ",
            "Programs can no longer be started from ", "Δεν ξεκινούν πλέον προγράμματα από το ",
            "Not cleaned (outside the student's profile or an unusual name): ", "Δεν καθαρίζεται (εκτός προφίλ μαθητή ή με ασυνήθιστο όνομα): ",
            "This PC now uses the DNS server ", "Αυτό το PC χρησιμοποιεί τώρα τον διακομιστή DNS ",
            ", which would be blocked. Tick the fixed IP or the DNS setting as well, so the PC uses the typed servers first.",
                ", που θα αποκλειόταν. Τσέκαρε και τη ρύθμιση της σταθερής IP ή του DNS, ώστε το PC να χρησιμοποιεί πρώτα τους διακομιστές που έγραψες.",
            "DNS is only allowed to ", "Το DNS επιτρέπεται μόνο προς ",
            "The folder with Windows' account pictures was not found: ", "Ο φάκελος με τις εικόνες λογαριασμών των Windows δεν βρέθηκε: ",
            " blocklist entries are now blocked on this PC.", " εγγραφές της λίστας είναι πλέον αποκλεισμένες σε αυτό το PC.",
            "Blocks every site in ", "Αποκλείει κάθε ιστότοπο του ",
            " on this PC (hosts file plus the block lists of Chrome, Edge, Brave and Firefox) and the browsers' built-in games. Edit the list, then apply again.",
                " σε αυτό το PC (αρχείο hosts και λίστες αποκλεισμού των Chrome, Edge, Brave και Firefox), καθώς και τα ενσωματωμένα παιχνίδια των περιηγητών. Άλλαξε τη λίστα και εφάρμοσε ξανά.",
            "The blocklist is empty. Add sites to ", "Η λίστα αποκλεισμού είναι άδεια. Πρόσθεσε ιστότοπους στο ",
            "The browsers' block lists were set, but the Windows hosts file could not be changed (", "Οι λίστες αποκλεισμού των περιηγητών ορίστηκαν, αλλά το αρχείο hosts των Windows δεν άλλαξε (",
            "). An antivirus program may be protecting it.", "). Ίσως το προστατεύει κάποιο αντιιικό.",
            "The computer name becomes ", "Το όνομα υπολογιστή γίνεται ",
            " after the restart.", " μετά την επανεκκίνηση.",
            "This PC already has the name ", "Αυτό το PC έχει ήδη το όνομα ",
            "Windows refused the new computer name: ", "Τα Windows αρνήθηκαν το νέο όνομα υπολογιστή: ",
            " is the network or broadcast address of that network and cannot be given to a PC.", " είναι η διεύθυνση δικτύου ή broadcast αυτού του δικτύου και δεν δίνεται σε PC.",
            "The gateway ", "Η πύλη ",
            " is not in the same network as ", " δεν είναι στο ίδιο δίκτυο με το ",
            ". Check the address, the mask and the gateway.", ". Έλεγξε τη διεύθυνση, τη μάσκα και την πύλη.",
            "Could not set the DNS server on '", "Δεν ορίστηκε ο διακομιστής DNS στο '",
            "Could not set the second DNS server on '", "Δεν ορίστηκε ο δεύτερος διακομιστής DNS στο '",
            "DNS on '", "Το DNS στο '",
            "' set to ", "' ορίστηκε σε ",
            "' was not changed by this setting, so it was left as it is.", "' δεν άλλαξε από αυτή τη ρύθμιση, γι' αυτό έμεινε όπως είναι.",
            "Could not set the DNS on '", "Δεν ορίστηκε το DNS στο '",
            "' back to automatic", "' ξανά σε αυτόματο",
            "' is automatic again.", "' είναι ξανά αυτόματο.",
            "' restored to ", "' επανήλθε σε ",
            "Another device on the network already answers on ", "Άλλη συσκευή στο δίκτυο απαντά ήδη στο ",
            ". Nothing was changed.", ". Δεν άλλαξε τίποτα.",
            "Could not set the IP address on '", "Δεν ορίστηκε η διεύθυνση IP στο '",
            "Could not set '", "Δεν ορίστηκε το '",
            " (mask ", " (μάσκα ",
            " (error ", " (σφάλμα ",
            "' now has the fixed address ", "' έχει τώρα τη σταθερή διεύθυνση ",
            ", gateway ", ", πύλη ",
            "' already gets its address automatically.", "' παίρνει ήδη αυτόματα τη διεύθυνσή του.",
            "' back to an automatic address", "' ξανά σε αυτόματη διεύθυνση",
            "' gets its address and DNS automatically (DHCP) again.", "' παίρνει ξανά αυτόματα διεύθυνση και DNS (DHCP).",
            "Could not open the student's settings file: ", "Δεν άνοιξε το αρχείο ρυθμίσεων του μαθητή: ",
            "Could not open the registry key ", "Δεν άνοιξε το κλειδί μητρώου ",
            "Could not create the registry key ", "Δεν δημιουργήθηκε το κλειδί μητρώου ",

            // startup page
            "Startup switched ON:  ", "Εκκίνηση ΕΝΕΡΓΗ:  ",
            "Startup switched OFF: ", "Εκκίνηση ΑΝΕΝΕΡΓΗ: ",
            "Startup program ", "Πρόγραμμα εκκίνησης ",
            "(All accounts", "(Όλοι οι λογαριασμοί",
            "(Student account", "(Λογαριασμός μαθητή",
            ", Startup folder)", ", φάκελος Εκκίνησης)",

            // health check
            "LabLock health check - ", "Έλεγχος υγείας LabLock - ",
            "The only administrator is ", "Ο μόνος διαχειριστής είναι ο ",
            "These accounts are administrators: ", "Αυτοί οι λογαριασμοί είναι διαχειριστές: ",
            ". There should be only the one you use.", ". Θα έπρεπε να είναι μόνο αυτός που χρησιμοποιείς.",
            "Switched on: ", "Ενεργοί: ",
            ". They are best left off.", ". Καλύτερα να μένουν ανενεργοί.",
            " is an administrator: students can install and change anything.", " είναι διαχειριστής: οι μαθητές μπορούν να εγκαταστήσουν και να αλλάξουν τα πάντα.",
            " is a standard user.", " είναι τυπικός χρήστης.",
            "This PC signs in automatically as ", "Αυτό το PC συνδέεται αυτόματα ως ",
            "Real-time protection is on.", "Η προστασία σε πραγματικό χρόνο είναι ενεργή.",
            "Real-time protection is OFF.", "Η προστασία σε πραγματικό χρόνο είναι ΑΝΕΝΕΡΓΗ.",
            " Definitions of ", " Ορισμοί της ",
            "The antivirus skips: ", "Το αντιιικό παραλείπει: ",
            ". Games and cracks often add themselves here.", ". Παιχνίδια και cracks συχνά προστίθενται μόνα τους εδώ.",
            "Off for these network types: ", "Ανενεργό για αυτούς τους τύπους δικτύου: ",
            "The last update was installed on ", "Η τελευταία ενημέρωση εγκαταστάθηκε στις ",
            " days ago).", " ημέρες πριν).",
            " GB free on ", " GB ελεύθερα στο ",
            ". These were installed without administrator rights; remove them while signed in as the student.", ". Εγκαταστάθηκαν χωρίς δικαιώματα διαχειριστή· αφαίρεσέ τα συνδεδεμένος ως μαθητής.",
            " program or installer files (.exe, .msi) are lying there.", " αρχεία προγραμμάτων ή εγκατάστασης (.exe, .msi) βρίσκονται εκεί.",
            " are switched on. See the Startup page.", " είναι ενεργά. Δες τη σελίδα Εκκίνηση.",
            " lines were not written by LabLock. Check them if you did not add them yourself.", " γραμμές δεν γράφτηκαν από το LabLock. Έλεγξέ τες αν δεν τις πρόσθεσες εσύ.",
            ". The first one is not the DNS server typed on the Lockdown page.", ". Ο πρώτος δεν είναι ο διακομιστής DNS που έγραψες στη σελίδα Κλείδωμα.",

            // silent run
            "No account named '", "Δεν υπάρχει λογαριασμός με όνομα '",
            "' on this PC: the settings for the student will fail.", "' σε αυτό το PC: οι ρυθμίσεις του μαθητή θα αποτύχουν.",
            "Skipped in a silent run (it differs per PC): ", "Παραλείφθηκε στην αθόρυβη εκτέλεση (διαφέρει ανά PC): ",
            "The silent run stopped: ", "Η αθόρυβη εκτέλεση σταμάτησε: "
        };
    }
}
