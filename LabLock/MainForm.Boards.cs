using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace LabLock
{
    /// <summary>
    /// The Boards page: for the Windows 11 module of an interactive board, which only teachers use.
    /// It has its own shut-down time, because a board is not a lab PC; the board's own Android side is
    /// outside the reach of a Windows program, so the page says what to set on the board itself.
    /// </summary>
    internal sealed partial class MainForm
    {
        private const string BoardDefaultTime = "21:00";

        private InputBox inBoardTime;
        private Label lblBoardState;

        private void BuildBoardsPage(Panel page)
        {
            // ---- the board's Windows
            Panel windows = AddCard(page, 16, 6, 476, 300);
            AddLabel(windows, "THE BOARD'S WINDOWS (WINDOWS 11 MODULE)", LabelX, 10, 440, RoleGroup);
            Label about = AddLabel(windows,
                "Run LabLock on the board itself. Teachers often forget the board on: every day at this time it shows a 5-minute warning and shuts down. Nothing else of the lab lockdown is applied here.",
                LabelX, 36, 448, RoleSubtle);
            about.Height = 72;

            AddLabel(windows, "Shut down at", LabelX, 121, 100, RoleSubtle);
            inBoardTime = AddInput(windows, 118, 116, 90);
            inBoardTime.Text = NightShutdownSetting.CleanTime(config.Get("boardshutdown", BoardDefaultTime));
            AddLabel(windows, "(24-hour time, for example 21:00)", 216, 121, 246, RoleSubtle);

            lblBoardState = AddLabel(windows, "", LabelX, 160, 448, null);
            lblBoardState.Height = 36;

            AddButton(windows, "Apply on this board", 14, 206, 220, 36, RolePrimary, OnBoardApplyClick);
            AddButton(windows, "Undo on this board", 242, 206, 220, 36, null, OnBoardUndoClick);

            Label tip = AddLabel(windows,
                "From the teacher PC, for many boards: LabLock.exe /apply /shutdown:21:00 (with only the night shut-down ticked).",
                LabelX, 252, 448, RoleSubtle);
            tip.Height = 40;

            // ---- the board itself
            Panel board = AddCard(page, 508, 6, 476, 300);
            AddLabel(board, "THE BOARD ITSELF (SCREEN AND ANDROID)", LabelX, 10, 440, RoleGroup);
            Label steps = AddLabel(board,
                "When Windows shuts down, only the Windows module is off: the screen and Android stay on. Set this once on every board, from its own menu (Android settings or the board's settings button):\r\n\r\n" +
                "1. A scheduled power-off. It is called Timer switch, Scheduled power off, Power schedule or Eco. Set it 10 minutes after the time on the left, every day.\r\n\r\n" +
                "2. If the board has it: switch off when there is no signal. Then the board also goes off by itself once Windows has shut down.\r\n\r\n" +
                "3. In the board's power options, choose that Windows starts with the board, so the next morning everything comes on with one button.",
                LabelX, 36, 448, null);
            steps.Height = 256;

            Label info = AddLabel(page,
                "The time is kept in LabLock.ini (boardshutdown=). The lab PCs keep their own time (shutdown=, 00:00), set on the Lockdown page. Restore this PC takes the board's shut-down back too.",
                16, 318, 968, RoleSubtle);
            info.Height = 40;
            info.Anchor = AnchorTopWide;
        }

        private void ShowBoardState()
        {
            try
            {
                RunContext c = new RunContext();
                c.Log = WriteLog;
                string state = new NightShutdownSetting().State(c);
                lblBoardState.Text = L.T("Now: ") + L.T(state);
            }
            catch (Exception ex)
            {
                lblBoardState.Text = L.T("Cannot check: ") + ex.Message;
            }
        }

        private void RunBoard(bool apply)
        {
            string typed = inBoardTime.Text.Trim();
            if (apply && !NightShutdownSetting.IsTime(typed))
            {
                Tell("Type the time as hours and minutes, for example 21:00.", MessageBoxIcon.Warning);
                return;
            }
            string time = NightShutdownSetting.CleanTime(typed.Length > 0 ? typed : BoardDefaultTime);
            inBoardTime.Text = time;

            string question = apply
                ? L.T("Shut this board's Windows down every day at ") + time + "?"
                : L.T("Remove the daily shut-down from this board?");
            if (!Confirm(question, MessageBoxIcon.Question)) return;

            RunContext c = BuildContext();   // with the student chosen on the Lockdown page, so the report to SNM is complete
            c.ShutdownTime = time;
            c.Restoring = !apply;

            List<Setting> list = new List<Setting>();
            list.Add(new NightShutdownSetting());
            int failed;
            Cursor = Cursors.WaitCursor;
            try
            {
                WriteLog("---- " + (apply ? "Applying on " : "Undoing on ") + ComputerName.Active + " ----");
                Engine.Run(list, c, apply, out failed);
            }
            finally
            {
                Cursor = Cursors.Default;
            }

            config.Set("boardshutdown", time);
            try
            {
                config.Save();
            }
            catch (Exception)
            {
            }
            ShowBoardState();
            if (rows.Count > 0) RefreshStates();   // the Lockdown checklist shows the night shut-down too
            ReportToSnm(c, "board");
            Tell(failed > 0 ? "Read the lines marked FAILED in the log." : (apply ? "Done. Now set the board's own power-off (right side of this page)." : "Done."),
                 failed > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
        }

        private void OnBoardApplyClick(object sender, EventArgs e)
        {
            RunBoard(true);
        }

        private void OnBoardUndoClick(object sender, EventArgs e)
        {
            RunBoard(false);
        }
    }
}
