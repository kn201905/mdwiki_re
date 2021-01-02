'use strict'

const g_FileLister = new function() {
	const IDX_path_depth = 2;
	const IDX_parent_dir_info = 3;
	const IDX_e_div_dirs = 4;
	const IDX_e_div_files = 5;

	let m_e_panel;
	let m_pnl_DirArea;
	let m_pnl_FileArea;

	// dir_info のフォーマット（dir_path の末尾は「/」）
	// str_dir_path がルート（./）である場合、親 dir_info は null
	// [str_dir_path, SEC_Updated, path_depth, 親 dir_info, e_div_dirs, e_div_files]
	const m_dirInfos = {};

	// -----------------------------------------------------------------------
	this.Set_e_panel = (e_div) => {
		m_e_panel = e_div;  // パネル全体に処理をすることを想定している（現在は未使用）

		// -----------------------------------------------
		m_pnl_DirArea = new function() {
			const m_e_panel = e_div.Add_Div();
			const ma_stages = [];

			this.Add = (dir_info) => {
				if (dir_info[IDX_e_div_dirs] == null) { return; }

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
				const path_depth = dir_info[IDX_path_depth];
				for (let depth = path_depth + 1; depth < ma_stages.length; ++depth) {
					ma_stages[depth].Hide();
				}

				let dir_info_to_show = dir_info;
				for (let depth = path_depth; depth >= 0; --depth) {
					ma_stages[depth].Show(dir_info_to_show);
					dir_info_to_show = dir_info_to_show[IDX_parent_dir_info];
				}
			};

			const Crt_NewStage = () => new function() {
				const m_e_stage = m_e_panel.Add_Div();
				let m_e_div_dirs_cur = null;
				const m_e_div_dirs_empty = m_e_stage.Add_DivTxt("no directories...");
				m_e_div_dirs_empty.style.display = 'none';
				m_e_div_dirs_empty.classList.add('Dir_List_Item');

				this.Add = (dir_info) => {
					m_e_stage.appendChild(dir_info[IDX_e_div_dirs]);
				};

				this.Show = (dir_info) => {
					if (m_e_div_dirs_cur != null) {
						m_e_div_dirs_cur.style.display = 'none';
					}

					m_e_div_dirs_cur = dir_info[IDX_e_div_dirs];
					if (m_e_div_dirs_cur != null) {
						m_e_div_dirs_cur.style.display = 'flex';
					}
					else {
						m_e_div_dirs_cur = m_e_div_dirs_empty;
						m_e_div_dirs_cur.style.display = 'block';
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
			let m_e_div_files_cur = null;
			const m_e_div_files_empty = m_e_panel.Add_DivTxt("no files...");
			m_e_div_files_empty.style.display = 'none';
			m_e_div_files_empty.classList.add('File_List_Item');

			this.Add = (dir_info) => {
				const e_div_files = dir_info[IDX_e_div_files];
				if (e_div_files != null) {
					m_e_panel.appendChild(e_div_files);
				}
			};

			this.Show = (dir_info) => {
				if (m_e_div_files_cur != null) {
					m_e_div_files_cur.style.display = 'none';
				}

				m_e_div_files_cur = dir_info[IDX_e_div_files];
				if (m_e_div_files_cur != null) {
					m_e_div_files_cur.style.display = 'flex';
				}
				else {
					m_e_div_files_cur = m_e_div_files_empty;
					m_e_div_files_cur.style.display = 'block';
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

		// 親 path_dir の取得
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

		if (path_depth == 0 || pcs_dirs > 0) {
			const e_div_dirs = document.createElement('div');
			e_div_dirs.style.display = 'none';
			e_div_dirs.style.flexWrap = 'wrap';

			if (path_depth == 0) {
				const e_div_dir_item_root = e_div_dirs.Add_DivTxt('/');
				e_div_dir_item_root.classList.add('Dir_List_Item');
				e_div_dir_item_root.onclick = OnClk_DirName.bind(null, dir_info, "");
			}

			for (let i = pcs_dirs; i > 0; --i) {
				if ((id = g_TStream_Reader.Consume_NextID()) != ID_Text) {
					g_pnl_Log.Log("!!! Consume_DirFileList() -> expect : str_dir_name");
					g_TStream_ID_to_Log.Show(id);
					return;
				}
				const str_dir_name = g_TStream_Reader.Get_text_cur();
				const e_div_dir_item = e_div_dirs.Add_DivTxt(str_dir_name);
				e_div_dir_item.classList.add('Dir_List_Item');
				e_div_dir_item.onclick = OnClk_DirName.bind(null, dir_info, str_dir_name);
			}
			dir_info.push(e_div_dirs);
		}
		else {
			// pcs_dirs == 0 のとき
			dir_info.push(null);
		}

		// ------------------------------------------------------
		// ID_File_Names の処理		
		if (g_TStream_Reader.Consume_NextID() != ID_File_Names) {
			g_pnl_Log.Log("!!! 不正なフォーマット / Consume_DirFileList() -> ID_File_Names が来るはず");
			return;
		}
		const pcs_files = g_TStream_Reader.Get_param_cur();

		if (pcs_files > 0) {
			const e_div_files = document.createElement('div');
			e_div_files.style.display = 'none';
			e_div_files.style.flexWrap = 'wrap';

			for (let i = pcs_files; i > 0; --i) {
				if (g_TStream_Reader.Consume_NextID() != ID_Text) {
					g_pnl_Log.Log("!!! 不正なフォーマット / Consume_DirFileList() -> ファイル名が来るはず");
					return;
				}
				const str_file_name = g_TStream_Reader.Get_text_cur();
				const e_div_file_item = e_div_files.Add_DivTxt(str_file_name);
				e_div_file_item.classList.add('File_List_Item');
				e_div_file_item.onclick = OnClk_FileName.bind(null, dir_info, str_file_name);
			}
			dir_info.push(e_div_files);
		}
		else {
			// pcs_files == 0 のとき
			dir_info.push(null);
		}

		// End の確認
		if (g_TStream_Reader.Consume_NextID() != ID_End) {
			g_pnl_Log.Log("!!! 不正なフォーマット / Consume_DirFileList() -> ID_End が来るはず");
			return;
		}

		// ID_DirFileList の正しいフォーマットが確認できた
		m_dirInfos[str_dir_path] = dir_info;
		m_pnl_DirArea.Add(dir_info);
		m_pnl_FileArea.Add(dir_info);

		Show_DirTree(str_dir_path);
	};

	// -----------------------------------------------------------------------
	const Show_DirTree = (str_dir_path) => {
		const dir_info = m_dirInfos[str_dir_path];
		if (dir_info == undefined) {
			g_pnl_Log.Log("!!! Show_DirTree() : dir_info == undefined");
			return;
		}

		m_pnl_DirArea.Show(dir_info);
		m_pnl_FileArea.Show(dir_info);
	};

	// -----------------------------------------------------------------------
	const OnClk_DirName = (dir_info, dir_name) => {
		console.log(dir_info);
		console.log(dir_name);
	};

	// -----------------------------------------------------------------------
	const OnClk_FileName = (dir_info, file_name) => {
		console.log(dir_info);
		console.log(file_name);
	};
};

