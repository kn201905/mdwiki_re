#define ACTIVATE_SERVER
//#define CREATE_TEST_LEXED_CODE
//#define TEST_Set_DirFileNames

using System;
using System.Text;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

// この呼び出しを待たないため、呼び出しの結果に 'await' 演算子を適用することを検討してください。
#pragma warning disable CS4014
// この非同期メソッドには 'await' 演算子がないため、同期的に実行されます。
#pragma warning disable CS1998


namespace md_svr
{
	// ＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜
	// FileLister.INtfy_DeleteFile の実装は仮実装
	// 本実装では、MD ファイルを管理するクラスに付与すること
	public partial class MainForm : Form, FileLister.INtfy_DeleteFile
	{
		// ＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜
		public void Ntfy_DeleteFile(string path)
		{
			MainForm.DBG_StdOut($"【Ntfy_DeleteFile】path-> {path}\r\n");
		}



		// リソースの節約
		static public Font ms_meiryo_Ke_P_9pt = null;
		static public Font ms_meiryo_8pt = null;

		// false: little endian / true: BOM付加 / true: 例外スローあり
		public static UnicodeEncoding ms_utf16_encoding = new UnicodeEncoding(false, true, true);

		// ---------------------------------------------------------
		static RichTextBox ms_RBox_stdout = null;

		// ---------------------------------------------------------
#if ACTIVATE_SERVER
		static Task ms_task_MdSvr;
#endif

		// ＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜
#if CREATE_TEST_LEXED_CODE
		static byte[] ms_DBG_ws_buf = new byte[100 * 1024];
		public static Write_WS_Buffer ms_DBG_write_WS_buf = new Write_WS_Buffer(ms_DBG_ws_buf);
#endif

#if TEST_Set_DirFileNames
		static byte[] ms_TEST_SetDirFilesNames_ary_buf = new byte[10 * 1024];  // 10 kbytes
		public static Write_WS_Buffer ms_TEST_SetDirFilesNames_WS_buf
				= new Write_WS_Buffer(ms_TEST_SetDirFilesNames_ary_buf);
#endif
		// ------------------------------------------------------------------------------------
		public MainForm()
		{
			InitializeComponent();

			// リソース節約のためのコード
			ms_meiryo_Ke_P_9pt = new Font("MeiryoKe_PGothic", 9F, FontStyle.Regular, GraphicsUnit.Point, ((byte)(128)));
			ms_meiryo_8pt = new Font("メイリオ", 8.25F, FontStyle.Regular, GraphicsUnit.Point, ((byte)(128)));

			m_Btn_close.Font = ms_meiryo_Ke_P_9pt;
			m_RBox_stdout.Font = ms_meiryo_8pt;

			// FileLister の static 変数の設定
//			FileLister.ms_utf16_encoding = ms_utf16_encoding;

			// ---------------------------------------------------------
			ms_RBox_stdout = m_RBox_stdout;
			m_RBox_stdout.SelectionTabs = new int[] { 30 };
			m_RBox_stdout.LanguageOption = RichTextBoxLanguageOptions.UIFonts;  // 行間を狭くする

			// ---------------------------------------------------------
			m_Btn_close.Click += OnClk_Close;

			// ---------------------------------------------------------
			System.IO.Directory.SetCurrentDirectory("md_root");
			FileLister.ms_INtfy_DeleteFile = this;

#if CREATE_TEST_LEXED_CODE
			try
			{
				Lexer.LexFile(ms_DBG_write_WS_buf, "md_root/index.md");
				StdOut("--- Lexing 処理完了\r\n");

				ms_DBG_write_WS_buf.Simplify_Buf();
				StdOut("--- Simplify_Buf 処理完了\r\n");
			}
			catch(Exception ex)
			{
				// 例外が発生した場合でも、ms_DBG_write_WS_buf には、エラーが発生した場所までの
				// Lexing 結果が入っている
				StdOut($"!!! 例外を補足しました : {ex.Message}\r\n");
				return;
			}

			DBG_WS_Buffer.Show_WS_buf(ms_RBox_stdout, ms_DBG_ws_buf, ms_DBG_write_WS_buf.Get_idx_byte_cur());
#endif

#if ACTIVATE_SERVER
			MdSvr.ms_utf16_encoding = ms_utf16_encoding;
			ms_task_MdSvr = MdSvr.Spawn_Start();
#endif
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
		public static void DBG_StdOut(string msg)
		{
			ms_RBox_stdout.SelectionColor = System.Drawing.Color.FromArgb(255, 80, 0);
			ms_RBox_stdout.AppendText(DateTime.Now.ToString("[HH:mm:ss]　"));
			ms_RBox_stdout.AppendText(msg);
			ms_RBox_stdout.SelectionColor = System.Drawing.Color.Black;
		}
		
		// ------------------------------------------------------------------------------------
		async void OnClk_Close(object sender, EventArgs e)
		{
#if ACTIVATE_SERVER
			m_Btn_close.Enabled = false;
			MainForm.StdOut("--- サーバーのシャットダウンシグナルを送信しました\r\n");

			MdSvr.SendSignal_Shutdown();
			await ms_task_MdSvr;
#endif
			this.Close();  // MainForm の Close
		}

		// ------------------------------------------------------------------------------------
		private void OnClk_ClearLog(object sender, EventArgs e)
		{
			ms_RBox_stdout.Clear();
		}

		// ------------------------------------------------------------------------------------
		private void OnClk_Test(object sender, EventArgs e)
		{
#if TEST_Set_DirFileNames
			FileLister.Set_DirFileNames(ms_TEST_SetDirFilesNames_WS_buf, "./");
#endif
		}

		// ------------------------------------------------------------------------------------
		public static void HexDump(byte[] buf, uint idx, uint pcs)
		{
			string str = "";
			for (; pcs > 0; --pcs)
			{
				str += buf[idx++].ToString("x2") + " ";
			}
			StdOut(str + "\r\n");
		}
	}
}
