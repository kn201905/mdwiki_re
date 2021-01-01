#define DBG_LOG_FileLister

using System;
using System.Text;
using System.IO;
using System.Collections.Generic;

namespace md_svr
{

static class FileLister
{
	// 存在しなくなったファイルの通知に利用される（保持している md データの dispose に利用する）
	public interface INtfy_DeleteFile
	{
		void Ntfy_DeleteFile(string path);
	}
	static public INtfy_DeleteFile ms_INtfy_DeleteFile = null;

	///////////////////////////////////////////////////////////////////////////////////////

	// ディレクトリパス -> DirInfo
	static SortedDictionary<string, DirInfo> ms_DirInfos = new SortedDictionary<string, DirInfo>();

	class DirInfo
	{
		static long ms_base_tick = (new DateTime(2020, 1, 1)).Ticks;

		// SEC は、ms_base_tick からの経過秒
		int m_SEC_Updated = (int)((DateTime.Now.Ticks - ms_base_tick) / 10_000_000);

		// ディレクトリ名のリスト（ディレクトリの変更は検知しない仕様にした。ディレクトリツリーの変更検知は大変なため、、）
		// 一度生成されたら、変更されることは想定していない
		public SortedSet<string> m_dirs_in_dir = new SortedSet<string>();

		// ファイル名のリスト（存在確認の uint 値を持たせている）
		uint m_exist_check_cnt = 0;
		public SortedList<string, uint> m_files_in_dir = new SortedList<string, uint>();

		// ------------------------------------------------------------------------------------
		// m_files_in_dir から情報を削除するために利用される
		static int ms_remove_idx_ary_pcs = 10;
		static int[] msa_remove_idx_ary = new int[10];  // 暫定実装： 配列が不足したら 10 ずつ増やすことにした

		// ------------------------------------------------------------------------------------
		// m_SEC_Updated 時刻以降に、ディレクトリに変化が検出された場合 true が返される
		public bool IsNeed_Update(string path_dir)
		{
			int SEC_wrt_tick = (int)((Directory.GetLastWriteTime(path_dir).Ticks - ms_base_tick) / 10_000_000);
			return (SEC_wrt_tick > m_SEC_Updated);
		}

		// ------------------------------------------------------------------------------------
		// m_files_in_dir のみが Update される（m_dirs_in_dir はチェックされないため注意）
		public void Update(Write_WS_Buffer send_WS_buf, string path_dir)
		{
			m_SEC_Updated = (int)((DateTime.Now.Ticks - ms_base_tick) / 10_000_000);

			// ---------------------------------------------------------
			// ディレクトリ名の設定
			send_WS_buf.Wrt_ID_param(ID.Directory_Names, (byte)m_dirs_in_dir.Count);
			foreach (string dname in m_dirs_in_dir) { send_WS_buf.Wrt_PStr(dname); }

			// ---------------------------------------------------------
			// ファイル名の設定
			{
				// ID.File_Names は、ディレクトリの個数が分かってから書き込む
				int idx_byte_AtFName = send_WS_buf.Get_idx_byte_cur();
				send_WS_buf.Skip_Wrt_ID();

				var files = Directory.EnumerateFiles(path_dir);
				int cnt = 0;
				m_exist_check_cnt++;  // 存在確認用のカウンタを１つ進める
				foreach(string fpath in files)
				{
					// md ファイル以外であれば処理しない
					if (IsMdFile(fpath) == false) { continue; }

					string fname = Get_FName(fpath);

					if (m_files_in_dir.ContainsKey(fname) == true)
					{
						m_files_in_dir[fname] = m_exist_check_cnt;
					}
					else
					{
						m_files_in_dir.Add(fname, m_exist_check_cnt);
#if DBG_LOG_FileLister
						MainForm.DBG_StdOut($"【DBG_LOG_FileLister】Update() でファイル追加 -> {fname}\r\n");
#endif			
					}

					send_WS_buf.Wrt_PStr(fname);
					cnt++;
				}
				if (cnt > 255) { throw new Exception("ファイルの個数が 255 個を超えています。"); }

				// cnt == 0 であっても、ファイルの削除処理が必要である場合もあるため注意
				send_WS_buf.Wrt_ID_param_At(idx_byte_AtFName, ID.File_Names, (byte)cnt);

				// 存在しなくなったファイル名を削除する
				if (m_files_in_dir.Count == 0) { return; }  // m_files_in_dir が空であるときは、何もしなくて良い

				var it = m_files_in_dir.GetEnumerator();
				int idx_of_files = 0;
				int rem_remove_idx_ary = ms_remove_idx_ary_pcs;
				int idx_next_on_rmv_idx_ary = 0;

				// イテレータを利用している間はコンテナの削除等ができないため、削除対象の idx のみを記録していく
				while (it.MoveNext())
				{
					if (it.Current.Value != m_exist_check_cnt)
					{
						if (rem_remove_idx_ary == 0)
						{
							ms_remove_idx_ary_pcs += 10;
							Array.Resize(ref msa_remove_idx_ary, ms_remove_idx_ary_pcs);
							rem_remove_idx_ary = 10;
						}

						msa_remove_idx_ary[idx_next_on_rmv_idx_ary++] = idx_of_files;
						rem_remove_idx_ary--;

						// リムーブ対象に登録した時点で、リムーブされることを通知する
						// もし、Lexed されたデータが残っていたら削除すること
						ms_INtfy_DeleteFile.Ntfy_DeleteFile(path_dir + it.Current.Key);
					}

					idx_of_files++;
				}

				// リムーブを実行する
				// RemoveAt は後ろ側から実行する必要がある（前の方を削除すると、インデックス値が変わるため）
				for (int idx_on_rmv_idx_ary = idx_next_on_rmv_idx_ary; idx_on_rmv_idx_ary > 0 ;)
				{
					int idx_of_files_to_rmv = msa_remove_idx_ary[--idx_on_rmv_idx_ary];
					m_files_in_dir.RemoveAt(idx_of_files_to_rmv);
				}
			}
		}

		// ------------------------------------------------------------------------------------
		// デバッグ用
		public void Show_toStdOut()
		{
			string str_show = $"m_SEC_Updated -> {m_SEC_Updated.ToString()} \r\n　　　m_dirs_in_dir -> ";

			foreach (string dname in m_dirs_in_dir) { str_show += dname + " / "; }

			str_show += "\r\n　　　m_files_in_dir -> ";
			foreach (var fdata in m_files_in_dir) { str_show += $"{fdata.Key}, {fdata.Value.ToString()} / "; }
			MainForm.StdOut(str_show + "\r\n");
		}
	}

	///////////////////////////////////////////////////////////////////////////////////////

	static unsafe bool IsMdFile(string fname)
	{
		int pos_ext = fname.Length - 3;
		if (pos_ext < 1) { return false; }

		fixed (char* fname_top = fname)
		{
			ulong ui64_ext = *(ulong*)(fname_top + pos_ext);
			if (ui64_ext == 0x0000_0064_006d_002e)
			{ return true; }
			else
			{ return false; }
		}
	}

	// ------------------------------------------------------------------------------------
	// path から「/」以降のみが取り出される
	static unsafe string Get_FName(string path)
	{
		fixed (char* path_top = path)
		{
			char* psrc = path_top + path.Length;
			while (*--psrc != '/')
			{
				if (psrc == path_top) { return path; }
			}

			// *psrc == '/'
			psrc++;
			return new string(psrc, 0, path.Length - (int)(psrc - path_top));
		}
	}

	// ------------------------------------------------------------------------------------
	// DirInfo を生成すると同時に、send_WS_buf にも情報をセットしてしまう
	static DirInfo Crt_DirInfo(Write_WS_Buffer send_WS_buf, string path_dir)
	{
		DirInfo ret_dirinfo = new DirInfo();

		// ---------------------------------------------------------
		// ディレクトリ名の設定
		{
			// ID.Directory_Names は、ディレクトリの個数が分かってから書き込む
			int idx_byte_AtDNames = send_WS_buf.Get_idx_byte_cur();
			send_WS_buf.Skip_Wrt_ID();

			var dirs = Directory.EnumerateDirectories(path_dir);
			int cnt = 0;
			foreach(string dpath in dirs)
			{
				string dname = Get_FName(dpath);

				ret_dirinfo.m_dirs_in_dir.Add(dname);
				send_WS_buf.Wrt_PStr(dname);
				cnt++;
			}
			if (cnt > 255) { throw new Exception("ディレクトリの個数が 255 個を超えています。"); }

			send_WS_buf.Wrt_ID_param_At(idx_byte_AtDNames, ID.Directory_Names, (byte)cnt);
		}

		// ---------------------------------------------------------
		// ファイル名の処理
		{
			// ID.File_Names は、ディレクトリの個数が分かってから書き込む
			int idx_byte_AtFName = send_WS_buf.Get_idx_byte_cur();
			send_WS_buf.Skip_Wrt_ID();

			var files = Directory.EnumerateFiles(path_dir);
			int cnt = 0;
			foreach(string fpath in files)
			{
				// md ファイル以外であれば処理しない
				if (IsMdFile(fpath) == false) { continue; }

				string fname = Get_FName(fpath);

				// m_exit_check_cnt の初期値は 0
				ret_dirinfo.m_files_in_dir.Add(fname, 0);
				send_WS_buf.Wrt_PStr(fname);
				cnt++;
			}
			if (cnt > 255) { throw new Exception("ファイルの個数が 255 個を超えています。"); }

			send_WS_buf.Wrt_ID_param_At(idx_byte_AtFName, ID.File_Names, (byte)cnt);
		}

#if DBG_LOG_FileLister
		MainForm.DBG_StdOut($"【DBG_LOG_FileLister】Crt_DirInfo() がコールされました。 path_dir: {path_dir}\r\n");
		ret_dirinfo.Show_toStdOut();
#endif
		return ret_dirinfo;
	}

	// ------------------------------------------------------------------------------------
	// path_dir は、md_root からのパス。必ず「"./"」から始まる名前を渡すこと
	// path_dir = "./" とすると、"md_root/" 内の結果が送信される
	public static void Set_DirFileNames(Write_WS_Buffer send_WS_buf, string path_dir)
	{
		send_WS_buf.Flush();
		send_WS_buf.Wrt_ID(ID.FileList);
		send_WS_buf.Wrt_PStr(path_dir);

		try
		{
			if (ms_DirInfos.TryGetValue(path_dir, out DirInfo dir_info) == false)
			{
				dir_info = Crt_DirInfo(send_WS_buf, path_dir);
				ms_DirInfos.Add(path_dir, dir_info);
			}
			else
			{
				if (dir_info.IsNeed_Update(path_dir) == true)
				{
					dir_info.Update(send_WS_buf, path_dir);
				}
				else
				{
#if DBG_LOG_FileLister
					MainForm.DBG_StdOut("【DBG_LOG_FileLister】dir_info の Update 必要なし\r\n");
//					dir_info.Show_toStdOut();
#endif
					// ディレクトリ名の設定
					send_WS_buf.Wrt_ID_param(ID.Directory_Names, (byte)dir_info.m_dirs_in_dir.Count);
					foreach (string dname in dir_info.m_dirs_in_dir) { send_WS_buf.Wrt_PStr(dname); }

					// ファイル名の設定
					send_WS_buf.Wrt_ID_param(ID.File_Names, (byte)dir_info.m_files_in_dir.Count);
					foreach (var file_kvp in dir_info.m_files_in_dir) { send_WS_buf.Wrt_PStr(file_kvp.Key); }
				}
			}
		}
		catch (Exception ex)
		{
			MainForm.StdOut($"!!! 例外を補足しました : {ex.ToString()}\r\n");
		}

		send_WS_buf.Wrt_ID_End();
	}


} // static class FileLister
} // md_svr
