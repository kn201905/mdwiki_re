using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

// この呼び出しを待たないため、呼び出しの結果に 'await' 演算子を適用することを検討してください。
#pragma warning disable CS4014
// この非同期メソッドには 'await' 演算子がないため、同期的に実行されます。
#pragma warning disable CS1998

namespace md_svr
{
	public partial class MainForm : Form
	{
		// リソースの節約
		static public Font ms_meiryo_Ke_P_9pt = null;
		static public Font ms_meiryo_8pt = null;

		// ---------------------------------------------------------
		static RichTextBox ms_RBox_stdout = null;

		// ---------------------------------------------------------
		static Task ms_task_MdSvr;

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

			ms_task_MdSvr = MdSvr.Spawn_Start();
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

			MdSvr.SendSignal_Shutdown();
			await ms_task_MdSvr;

			this.Close();  // MainForm の Close
		}

		// ------------------------------------------------------------------------------------
		private void OnClk_clear(object sender, EventArgs e)
		{
			ms_RBox_stdout.Clear();
		}

		// ------------------------------------------------------------------------------------
		private void button1_Click(object sender, EventArgs e)
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
