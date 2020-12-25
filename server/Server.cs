//using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace md_svr
{
	static class MdSvr
	{
		// WebSocket �p�̃|�[�g�ԍ�
		enum WS_Port : uint { EN_num = 3000 }
		static string ms_str_port_num = ((uint)WS_Port.EN_num).ToString();

		// -------------------------------------------------
		public static CancellationTokenSource ms_cts_shutdown;

		// false: little endian / true: BOM�t�� / true: ��O�X���[����
		public static UnicodeEncoding ms_utf16_encoding = new UnicodeEncoding(false, true, true);

		// ------------------------------------------------------------------------------------
		public static async Task Spawn_Start()
		{
			var listener = new HttpListener();
			var tasks_context = new List<Task>();

			listener.Prefixes.Add($"http://localhost:{ms_str_port_num}/");
			MainForm.StdOut($"--- �ڑ���t�J�n�i�|�[�g: {ms_str_port_num}�j\r\n");

			listener.Start();
			using (ms_cts_shutdown = new CancellationTokenSource())
			{
				// WS_Context �� static �ϐ���ݒ�
				WS_Context.ms_cts_shutdown = ms_cts_shutdown;
				WS_Context.ms_utf16_encoding = ms_utf16_encoding;

				// Read_Buffer �� static �ϐ���ݒ�
				Read_Buffer.ms_utf16_encoding = ms_utf16_encoding;

				while (true)
				{
					// GetContextAsync() �́A�L�����Z���g�[�N�����T�|�[�g���Ă��Ȃ�
					HttpListenerContext context = await listener.GetContextAsync();
					if (context.Request.IsWebSocketRequest == false)
					{
						// ���̏ꍇ�A�T�[�o�[���I�����郁�b�Z�[�W���󂯎�������̂Ƃ���
						// GetContextAsync() �ɃL�����Z���g�[�N�����Ȃ����߂̑[�u
						MainForm.StdOut("--- �V���b�g�_�E�������J�n\r\n");

						ms_cts_shutdown.Cancel();

						foreach(Task task in tasks_context)
						{ await task; }

						break;
					}

					var ws_context = new WS_Context();
					tasks_context.Add(ws_context.Spawn_Context(context));
				}
			}

			listener.Stop();
			await Task.Delay(1000);  // ���ꂪ�����Ɨ�O���������邱�Ƃ�����悤�ȁH
			listener.Close();
		}

		// ------------------------------------------------------------------------------------
		public static void SendSignal_Shutdown()
		{
			// �����_�ł́A"CLOSE" �̕�����͔C�ӂ̂��̂ł悢
			var http_content = new StringContent("CLOSE", ms_utf16_encoding);
			new HttpClient().PostAsync($"http://localhost:{ms_str_port_num}/", http_content);
		}
	}
}

