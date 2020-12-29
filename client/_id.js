
// -----------------------------------------------------------------------
// 上り、下り両用のコード
const ID_Directory_Names = 1
const ID_File_Names = 2
const ID_End = 3

const ID_Text = 64

// -----------------------------------------------------------------------
// Param に Succeed or Fail を付加する
// クライアントが Lexed_MD を受信したときに、エラーがあった場合、ページ先頭に警告を表示したいため
const ID_Lexed_MD = 10
const ID_ERR_OVERFLOW = 11
// このマーク以降に、テキスト情報（最大文字数 EN_MAX_ERR_Text）が書き込まれている
// ERR_Report のマークの後に ID.Div、ID.Text が続く
// 特に、埋め込みメッセージがある場合は、ID.Div、ID.Text が２つ続く
const ID_ERR_Report = 12
const ID_ERR_on_Simplify = 13

const ID_BR = 20  // 特殊扱い

// ----------------------------------------------------------
// 行頭ブロック用
const ID_Div = 128
const ID_Div_Head = 129  // param に 1 - 6 が指定される
const ID_Div_Code = 130
const ID_Div_Quote = 131




// -----------------------------------------------------------------------
// Param
const Param_Succeeded = 1
const Param_Failed = 2

const Param_no_BR_above = 1 << 7;
