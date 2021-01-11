
// -----------------------------------------------------------------------
// 上り、下り両用のコード
const ID_Directory_Names = 1  // param -> ディレクトリ数
const ID_File_Names = 2  // param -> ファイル数
const ID_End = 3

const ID_Num_int = 4

// [req] ID_DirFileList -> ID_Text (path_dir)
// [reply] ID_DirFileList
// -> ID_Text (path_dir) -> ID_Text (親 path_dir) -> ID_Num_int (SEC_Updated)
// -> ID_Directory_Names -> ID_File_Names
const ID_DirFileList = 10  // param -> path_depth

// [req] ID_Files_inDir -> ID_Text (path_dir) -> ID_Num_int（現在保持している SEC）
// [replay] ID_Files_inDir -> ID_Text (path_dir)
// 更新不要の場合 -> ID_End
// 更新ありの場合 -> ID_Num_int (SEC_Updated) -> ID_File_Names
const ID_Files_inDir = 11

// [req] ID_MD_file -> ID_Text (path_dir) -> ID_Text (file name) (注：.md は省略されている)
// -> ID_Num_int (SEC_Updated) (データを持っていない場合、0 が設定される)
// [reply] ID_MD_file -> ID_Text (path_dir) -> ID_Text (file name) 
// 更新不要の場合 -> ID_End
// 更新ありの場合 -> ID_Num_int (SEC_Created) -> ID_Lexed_MD
// ファイルが削除されている場合 -> ID_MD_file_Deleted
const ID_MD_file = 12
const ID_MD_file_Deleted = 13

const ID_Text = 64

// -----------------------------------------------------------------------
// Param に Succeed or Fail を付加する
// Lexed_MD にエラーがあった場合、ページ先頭に警告を表示したいため
const ID_Lexed_MD = 20
const ID_ERR_OVERFLOW = 21
// このマーク以降に、テキスト情報（最大文字数 EN_MAX_ERR_Text）が書き込まれている
// ERR_Report のマークの後に ID.Div、ID.Text が続く
// 特に、埋め込みメッセージがある場合は、ID.Div、ID.Text が２つ続く
const ID_ERR_Report = 22
const ID_ERR_on_Simplify = 23


// 特殊扱い
const ID_BR = 30
const ID_HLine = 31

const ID_Table = 32
const ID_TR = 33
const ID_TD = 34

// ----------------------------------------------------------
// インライン用
const TxtFLG_mask = 0x3f
const TxtFLG_Bold = 1
const TxtFLG_Cancel = 2
const TxtFLG_Under = 4
const TxtFLG_Code = 8

// ----------------------------------------------------------
// 行頭ブロック用
// 注意： param を Param_no_BR_above 情報としても利用している
const ID_Div = 128
const ID_Div_Head = 129  // param に 1 - 6 が指定される
const ID_Div_Code = 130
const ID_Div_Quote = 131
const ID_Div_Bullet = 132



// -----------------------------------------------------------------------
// Param
const Param_Succeeded = 1
const Param_Failed = 2

const Param_no_BR_above = 1 << 7;
const Param_no_BR_above_mask = 0x7f;
