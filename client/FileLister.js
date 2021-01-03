'use strict'

const g_FileLister = new function() {
	const IDX_str_dir_path = 0;
	const IDX_SEC_Updated = 1;
	const IDX_path_depth = 2;
	const IDX_parent_dir_info = 3;  // 結局未使用になったかも、、、
	const IDX_e_div_dir_selected = 4;
	const IDX_e_div_dirs = 5;
	const IDX_e_div_file_selected = 6;
	const IDX_e_div_files = 7;

	let m_e_panel;
	let m_pnl_DirArea;
	let m_pnl_FileArea;
	let m_e_div_root_dir; 

	// dir_info のフォーマット（dir_path の末尾は「/」）
	// str_dir_path がルート（./）である場合、親 dir_info は null
	// [str_dir_path, SEC_Updated, path_depth, 親 dir_info
	//		e_div_dir_selected, e_div_dirs, e_div_file_selected, e_div_files]
	const m_dirInfos = {};

	// -----------------------------------------------------------------------
	this.Set_e_panel = (e_div) => {
		m_e_panel = e_div;  // パネル全体に処理をすることを想定している（現在は未使用）

		// -----------------------------------------------
		m_pnl_DirArea = new function() {
			const m_e_panel = e_div.Add_Div();
			const ma_stages = [];

			// Add されたものは、自動的に表示対象とする
			this.Add = (dir_info) => {
				const path_depth = dir_info[IDX_path_depth];
				// ma_stages.length >= path_depth + 1 であることが必要
				if (ma_stages.length < path_depth + 1) {
					ma_stages.push(Crt_NewStage());

					if (ma_stages.length < path_depth + 1) {
						g_pnl_Log.Log("!!! m_pnl_DirArea.Add() : ma_stages.length < path_depth + 1");
						return;
					}
				}
				ma_stages[path_depth].Add(dir_info);
			};

			this.Show = (dir_info) => {
				const path_depth = dir_info[IDX_path_depth];  // path_depth == ma_stages の idx
				for (let depth = path_depth + 1; depth < ma_stages.length; ++depth) {
					ma_stages[depth].Hide();
				}

				// path_depth == 0 ->「/」がクリックされたということ
				if (path_depth == 0) { return; }
				
				ma_stages[path_depth].Show(dir_info);
			};
/*
			// 「/」が押されたときのみ、特殊な操作となる
			this.Show_byRootBtn = (dir_info) => {
				for (let depth = ma_stages.length - 1; depth > 0; --depth) {
					console.log(depth);
					ma_stages[depth].Hide();
				}
				m_pnl_FileArea.Show(dir_info);
			};
*/
			const Crt_NewStage = () => new function() {
				const m_e_stage = m_e_panel.Add_Div();
				let m_e_div_dirs_showed = null;

				const m_e_div_dirs_empty = m_e_stage.Add_DivTxt("no directories...");
				m_e_div_dirs_empty.style.display = 'none';
				m_e_div_dirs_empty.classList.add('Dir_li');

				// Add されたものは、自動的に表示対象とする
				this.Add = (dir_info) => {
					m_e_stage.style.display = 'block';

					let e_div_showing = dir_info[IDX_e_div_dirs];
					if (e_div_showing == null) {
						if (m_e_div_dirs_showed == m_e_div_dirs_empty) { return; }

						e_div_showing = m_e_div_dirs_empty;
						m_e_div_dirs_empty.style.display = 'block';
					}
					else {
						m_e_stage.appendChild(e_div_showing);
					}

					if (m_e_div_dirs_showed != null) {
						m_e_div_dirs_showed.style.display = 'none';
					}
					m_e_div_dirs_showed = e_div_showing;
				};
				
				this.Show = (dir_info) => {
					m_e_stage.style.display = 'block';

					const e_div_showing = dir_info[IDX_e_div_dirs];
					if (e_div_showing == null) {
						// dir_info に含まれる dirs が空の場合
						if (m_e_div_dirs_showed == m_e_div_dirs_empty) { return; }
						if (m_e_div_dirs_showed != null) {
							m_e_div_dirs_showed.style.display = 'none';
						}

						m_e_div_dirs_empty.style.display = 'block';
						m_e_div_dirs_showed = m_e_div_dirs_empty;
						return;
					}
					else {
						// dir_info に１つ以上 dir が含まれている場合（dir_info[IDX_e_div_dirs] != null）
						if (m_e_div_dirs_showed == e_div_showing) { return; }

						// 現在表示中のものがあれば、それを隠す
						if (m_e_div_dirs_showed != null) {
							m_e_div_dirs_showed.style.display = 'none';
						}

						// 今まで画面に表示されていなかったものを表示するのであるから、
						// 選択中のアイテムがあったら、それを非選択にする。
						const e_div_dir_selected_in_showing = dir_info[IDX_e_div_dir_selected];
						if (e_div_dir_selected_in_showing != null) {
							e_div_dir_selected_in_showing.classList.remove('Dir_li_sel');
							dir_info[IDX_e_div_dir_selected] = null;
						}

						e_div_showing.style.display = 'flex';
						m_e_div_dirs_showed = e_div_showing;
					}
				};

				this.Hide = () => {
					m_e_stage.style.display = 'none';
				};
			}
		};

		// -----------------------------------------------
		m_pnl_FileArea = new function() {
			const m_e_panel = e_div.Add_Div();
			let m_e_div_files_showed = null;

			const m_e_div_files_empty = m_e_panel.Add_DivTxt("no files...");
			m_e_div_files_empty.style.display = 'none';
			m_e_div_files_empty.classList.add('File_li');

			// Add されたものは自動的に表示対象とする
			this.Add = (dir_info) => {
				const e_div_files_showing = dir_info[IDX_e_div_files];
				if (e_div_files_showing == null) {
					if (m_e_div_files_showed == m_e_div_files_empty) { return; }

					if (m_e_div_files_showed != null) {
						m_e_div_files_showed.style.display = 'none';
					}
					m_e_div_files_empty.style.display = 'block';
					m_e_div_files_showed = m_e_div_files_empty;
				}
				else {
					if (m_e_div_files_showed != null) {
						m_e_div_files_showed.style.display = 'none';
					}
					m_e_panel.appendChild(e_div_files_showing);
					m_e_div_files_showed = e_div_files_showing;
				}
			};

			this.Show = (dir_info) => {
				const e_div_files_showing = dir_info[IDX_e_div_files];
				if (e_div_files_showing == null) {
					// dir_info に含まれる files が空の場合
					if (m_e_div_files_showed == m_e_div_files_empty) { return; }
					if (m_e_div_files_showed != null) {
						m_e_div_files_showed.style.display = 'none';
					}

					m_e_div_files_empty.style.display = 'block';
					m_e_div_files_showed = m_e_div_files_empty;
					return;
				}
				else {
					// dir_info に１つ以上 file が含まれている場合（dir_info[IDX_e_div_files] != null）
					if (m_e_div_files_showed == e_div_files_showing) { return; }

					// 現在表示中のものがあれば、それを隠す
					if (m_e_div_files_showed != null) {
						m_e_div_files_showed.style.display = 'none';
					}

					// 今まで画面に表示されていなかったものを表示するのであるから、
					// 選択中のアイテムがあったら、それを非選択にする。
					const e_div_file_selected_in_showing = dir_info[IDX_e_div_file_selected];
					if (e_div_file_selected_in_showing != null) {
						e_div_file_selected_in_showing.classList.remove('File_li_sel');
						dir_info[IDX_e_div_file_selected] = null;
					}

					e_div_files_showing.style.display = 'flex';
					m_e_div_files_showed = e_div_files_showing;
				}
			};
		};
	};

	// -----------------------------------------------------------------------
	this.Consume_DirFileList = () => {
		let id;

		// path depth
		const path_depth = g_TStream_Reader.Get_param_cur();
		g_pnl_Log.Log('▶ path depth : ' + path_depth);

		// path_dir の取得
		if ((id = g_TStream_Reader.Consume_NextID()) != ID_Text) {
			g_pnl_Log.Log("!!! Consume_DirFileList() -> expect : dir_path");
			g_TStream_ID_to_Log.Show(id);
			return;
		}
		const str_dir_path = g_TStream_Reader.Get_text_cur();
		if (m_dirInfos[str_dir_path] != undefined) {
			g_pnl_Log.Log("!!! Consume_DirFileList() : m_dirInfos[str_dir_path] が既に定義済みとなっていた");
			return;
		}

		// path_depth > 0 の場合、親 path_dir の取得
		let parent_dir_info = null;
		if (path_depth > 0)
		{
			if ((id = g_TStream_Reader.Consume_NextID()) != ID_Text) {
				g_pnl_Log.Log("!!! Consume_DirFileList() -> expect : parent_dir_path");
				g_TStream_ID_to_Log.Show(id);
				return;
			}
			const str_parent_dir_path = g_TStream_Reader.Get_text_cur();
			parent_dir_info = m_dirInfos[str_parent_dir_path];
			if (parent_dir_info == undefined)
			{
				g_pnl_Log.Log("!!! Consume_DirFileList() : m_dirInfos[str_parent_dir_path] == undefined");
				g_pnl_Log.Log("　　　str_parent_dir_path : " + str_parent_dir_path);
				return;
			}
		}

		// SEC_Updated の処理
		if ((id = g_TStream_Reader.Consume_NextID()) != ID_Num_int) {
			g_pnl_Log.Log("!!! Consume_DirFileList() -> expect : SEC_Updated");
			g_TStream_ID_to_Log.Show(id);
			return;
		}
		// g_TStream_Reader.Get_param_cur() は SEC_Updated の値
		const dir_info = [str_dir_path, g_TStream_Reader.Get_param_cur(), path_depth, parent_dir_info];

		// ------------------------------------------------------
		// Directory_Names の処理（空の場合もあることに注意）
		if ((id = g_TStream_Reader.Consume_NextID()) != ID_Directory_Names) {
			g_pnl_Log.Log("!!! Consume_DirFileList() -> expect : ID_Directory_Names");
			g_TStream_ID_to_Log.Show(id);
			return;
		}
		const pcs_dirs = g_TStream_Reader.Get_param_cur();

//		const display_on_init = (path_depth == 0) ? 'flex' : 'none';
		if (path_depth == 0 || pcs_dirs > 0) {
			const e_div_dirs = document.createElement('div');
			e_div_dirs.style.display = 'flex';
//			e_div_dirs.style.display = display_on_init;
			e_div_dirs.style.flexWrap = 'wrap';

			if (path_depth == 0) {
				m_e_div_root_dir = e_div_dirs.Add_DivTxt('/');
				m_e_div_root_dir.classList.add('Dir_li');
				m_e_div_root_dir.classList.add('Dir_li_sel');
				m_e_div_root_dir.onclick = OnClk_DirName.bind(m_e_div_root_dir, dir_info, "");

				dir_info.push(m_e_div_root_dir);  // ルートだけは、最初から選択状態になっている
			}
			else {
				dir_info.push(null);
			}

			for (let i = pcs_dirs; i > 0; --i) {
				if ((id = g_TStream_Reader.Consume_NextID()) != ID_Text) {
					g_pnl_Log.Log("!!! Consume_DirFileList() -> expect : str_dir_name");
					g_TStream_ID_to_Log.Show(id);
					return;
				}
				const str_dir_name = g_TStream_Reader.Get_text_cur();
				const e_div_dir_item = e_div_dirs.Add_DivTxt(str_dir_name);
				e_div_dir_item.classList.add('Dir_li');
				e_div_dir_item.onclick = OnClk_DirName.bind(e_div_dir_item, dir_info, str_dir_name);
			}
			dir_info.push(e_div_dirs);
		}
		else {
			// path_depth != 0 && pcs_dirs == 0 のとき
			dir_info.push(null);
		}

		// ------------------------------------------------------
		// ID_File_Names の処理		
		if (g_TStream_Reader.Consume_NextID() != ID_File_Names) {
			g_pnl_Log.Log("!!! 不正なフォーマット / Consume_DirFileList() -> ID_File_Names が来るはず");
			return;
		}
		const pcs_files = g_TStream_Reader.Get_param_cur();
		dir_info.push(null);  // この時点で、選択されているファイルはない

		if (pcs_files > 0) {
			const e_div_files = document.createElement('div');
			e_div_files.style.display = 'flex';
//			e_div_files.style.display = display_on_init;
			e_div_files.style.flexWrap = 'wrap';

			for (let i = pcs_files; i > 0; --i) {
				if (g_TStream_Reader.Consume_NextID() != ID_Text) {
					g_pnl_Log.Log("!!! 不正なフォーマット / Consume_DirFileList() -> ファイル名が来るはず");
					return;
				}
				const str_file_name = g_TStream_Reader.Get_text_cur();
				const e_div_file_item = e_div_files.Add_DivTxt(str_file_name);
				e_div_file_item.classList.add('File_li');
				e_div_file_item.onclick = OnClk_FileName.bind(e_div_file_item, dir_info, str_file_name);
			}
			dir_info.push(e_div_files);
		}
		else {
			// pcs_files == 0 のとき
			dir_info.push(null);
		}

		// ID_DirFileList の情報取得完了
		m_dirInfos[str_dir_path] = dir_info;

		m_pnl_DirArea.Add(dir_info);  // Add されたものが、自動的に表示対象となることに注意
		m_pnl_FileArea.Add(dir_info);  // Add されたものが、自動的に表示対象となることに注意
	};

	// -----------------------------------------------------------------------
	this.Consume_Files_inDir = () => {
		let id;
		
			// path_dir の取得
		if ((id = g_TStream_Reader.Consume_NextID()) != ID_Text) {
			g_pnl_Log.Log("!!! Consume_Files_inDir() -> expect : dir_path");
			g_TStream_ID_to_Log.Show(id);
			return;
		}
		const str_dir_path = g_TStream_Reader.Get_text_cur();
		const dir_info = m_dirInfos[str_dir_path];
		if (dir_info == undefined) {
			g_pnl_Log.Log("!!! Consume_Files_inDir() : m_dirInfos[str_dir_path] が未定義");
			return;
		}
		
		id = g_TStream_Reader.Consume_NextID();
		if (id == ID_End) {
			g_pnl_Log.Log("▶ クライアント側の情報更新の必要なし　@FileLister.js");
			
			m_pnl_DirArea.Show(dir_info);
			m_pnl_FileArea.Show(dir_info);
		}
	};

	// -----------------------------------------------------------------------
	// this : 押された e_div
	// dir_info : this が所属する dir_info
	// dir_name : 押された e_div のディレクトリ名（= this.textContent）
	const OnClk_DirName = function(dir_info, dir_name) {
		const e_div_sel = dir_info[IDX_e_div_dir_selected];
		if (this == e_div_sel) { return; }

		// 選択状態の変更
		if (e_div_sel != null) {
			e_div_sel.classList.remove('Dir_li_sel');
		}
		this.classList.add('Dir_li_sel');
		dir_info[IDX_e_div_dir_selected] = this;

		// 指定されたディレクトリ内容の表示。ルートディレクトリのときのみ、特別な操作になる
		let str_dir_showing;
		let dir_info_showing;
		if (this == m_e_div_root_dir) {
//			m_pnl_DirArea.Show_byRootBtn(dir_info);
			str_dir_showing = './';
			dir_info_showing = m_dirInfos['./'];
		}
		else {
			// ディレクトリツリーを１階層下に進める処理
			str_dir_showing = dir_info[IDX_str_dir_path] + dir_name + '/';
			dir_info_showing = m_dirInfos[str_dir_showing];

			if (dir_info_showing == null) {
				// 情報が全くない場合の処理
				g_Write_Buf.Flush();
				g_Write_Buf.Wrt_ID(ID_DirFileList);
				g_Write_Buf.Wrt_PStr(str_dir_showing);

				g_WS.Send_Write_Buf();
				return;
			}
		}
		
		// dir_info_showing に情報が取得できた場合 -> ファイルの追加削除の変更を調べる
		g_Write_Buf.Flush();
		g_Write_Buf.Wrt_ID(ID_Files_inDir);
		g_Write_Buf.Wrt_PStr(str_dir_showing);
		g_Write_Buf.Wrt_Num_int(dir_info_showing[IDX_SEC_Updated]);
		
		g_WS.Send_Write_Buf();			
	};

	// -----------------------------------------------------------------------
	const OnClk_FileName = function(dir_info, file_name) {
		const e_div_sel = dir_info[IDX_e_div_file_selected];
		if (this == e_div_sel) { return; }

		// 選択状態の変更
		if (e_div_sel != null) {
			e_div_sel.classList.remove('File_li_sel');
		}
		this.classList.add('File_li_sel');
		dir_info[IDX_e_div_file_selected] = this;
	};
};

