#define ACTIVATE_SERVER
#define CREATE_TEST_LEXED_CODE

using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

// この呼び出しを待たないため、呼び出しの結果に 'await' 演算子を適用することを検討してください。
#pragma warning disable CS4014
// この非同期メソッドには 'await' 演算子がないため、同期的に実行されます。
#pragma warning disable CS1998


namespace md_svr
{
	internal partial class MainForm : Form
	{
		// リソースの節約
		static public Font ms_meiryo_Ke_P_9pt = null;
		static public Font ms_meiryo_8pt = null;

		// ---------------------------------------------------------
		static RichTextBox ms_RBox_stdout = null;

		// ---------------------------------------------------------
		static Task ms_task_MdSvr;


		// ＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜
#if CREATE_TEST_LEXED_CODE
		static byte[] ms_DBG_ws_buf = new byte[100 * 1024];
		public static Write_WS_Buffer ms_DBG_write_WS_buf = new Write_WS_Buffer(ms_DBG_ws_buf);
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

			// ---------------------------------------------------------
			ms_RBox_stdout = m_RBox_stdout;
			m_RBox_stdout.SelectionTabs = new int[] { 30 };
			m_RBox_stdout.LanguageOption = RichTextBoxLanguageOptions.UIFonts;  // 行間を狭くする

			// ---------------------------------------------------------
			m_Btn_close.Click += OnClk_Close;

		// ＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜＜
#if CREATE_TEST_LEXED_CODE
			try
			{
				Lexer.LexFile(ms_DBG_write_WS_buf, "md_root/index.md");
				StdOut("--- Lexing 処理完了\r\n");

				string ret_str = ms_DBG_write_WS_buf.Simplify_Buf();
				if (ret_str == null)
				{
					StdOut("--- Simplify_Buf 処理完了\r\n");
				}
				else
				{
					StdOut($"!!! Simplify_Buf でエラー検出 : {ret_str}\r\n");
				}
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
