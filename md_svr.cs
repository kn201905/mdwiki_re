
namespace md_svr
{
	public partial class MainForm : Form
	{
		static RichTextBox ms_RBox_stdout = null;

		// ---------------------------------------------------------
		static MdSvr ms_MdSvr;
		static Task ms_task_MdSvr;

		// ------------------------------------------------------------------------------------
		public MainForm()
		{
			InitializeComponent();

			// ------------------------------------------------------------------------------------
			ms_RBox_stdout = m_RBox_stdout;
			m_RBox_stdout.SelectionTabs = new int[] { 30 };
			m_RBox_stdout.LanguageOption = RichTextBoxLanguageOptions.UIFonts;  // 行間を狭くする

			// ---------------------------------------------------------
			m_Btn_close.Click += OnClk_Close;

			ms_MdSvr = new MdSvr();
			ms_task_MdSvr = ms_MdSvr.Spawn_Start();
		}

		// ------------------------------------------------------------------------------------
		public static void StdOut(string msg)
		{
			ms_RBox_stdout.SelectionColor = System.Drawing.Color.FromArgb(0, 150, 255);
			ms_RBox_stdout.AppendText(DateTime.Now.ToString("[HH:mm:ss]　"));
			ms_RBox_stdout.SelectionColor = System.Drawing.Color.Black;
			ms_RBox_stdout.AppendText(msg);
		}

		// ------------------------------------------------------------------------------------
		async void OnClk_Close(object sender, EventArgs e)
		{
			m_Btn_close.Enabled = false;
			MainForm.StdOut("--- サーバーのシャットダウンシグナルを送信しました\r\n");

			ms_MdSvr.SendSignal_Shutdown();
			await ms_task_MdSvr;

			this.Close();  // MainForm の Close
		}
	}
}
