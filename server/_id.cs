using System;

namespace md_svr
{

// セクション長が可変になるのは、Text のみとすること
[Flags] public enum ID : byte
{
	Undefined = 0,

	// ----------------------------------------------------------
	// 上り、下り両用のコード
	Directory_Names = 1,
	File_Names = 2,
	End = 3,
	FileList = 4,

	// Lexed の中では、Text は、Div のいずれかの中に必ず入ること
	// Text は、64 - 127
	Text = 64,

	// ----------------------------------------------------------
	// Param に Succeed or Fail を付加する
	// クライアントが Lexed_MD を受信したときに、エラーがあった場合、ページ先頭に警告を表示したいため
	Lexed_MD = 10,
	ERR_OVERFLOW = 11,
	// このマーク以降に、テキスト情報（最大文字数 EN_MAX_ERR_Text）が書き込まれている
	// ERR_Report のマークの後に ID.Div、ID.Text が続く
	// 特に、埋め込みメッセージがある場合は、ID.Div、ID.Text が２つ続く
	ERR_Report = 12,
	ERR_on_Simplify = 13,

	BR = 20,  // 特殊扱い
	HLine = 21,

	// ----------------------------------------------------------
	// インライン用
	Txt_flags_mask = 0b_0011_1111,
	Txt_Bold = 1,
	Txt_Cancel = 2,
	Txt_Under = 4,
	Txt_Code = 8,

	// ----------------------------------------------------------
	// Div用（128 - 191）
	Div = 128,
	Div_Head = 129,  // param に 1 - 6 が指定される
	Div_Code = 130,
	Div_Quote = 131,
	Div_Bullet = 132,
}

public static class ID_Ext
{
	public static bool IsText(this ID id)
	{ return id.HasFlag(ID.Text); }

	public static bool IsDiv(this ID id)
	{ return id.HasFlag(ID.Div); }
}

/////////////////////////////////////////////////////////////////////////////////////
// Param を利用するもの
//・Lexed_MD
//・Text : 255 文字以下の場合
//・Div_Head : 1 - 6
//・Div : bit 7 は、Div ブロックの上に空白を入れなくても良いことを示す

static class Param
{
	public const byte EN_Succeeded = 1;
	public const byte EN_Failed = 2;

	// 以下は Simplify_Buf() で設定される
	public const byte FLG_no_BR_above = 1 << 7;
	public const ushort FLG_no_BR_above_ushort = 1 << 15;
}

/////////////////////////////////////////////////////////////////////////////////////

static class Chr
{
	public const int PCS_Tab = 3;
	public const char TAB = (char)0x09;

	public const char LF = (char)0x0a;
	public const char CR = (char)0x0d;
	public const uint CRLF = (uint)0x000a_000d;

	public const char SP = (char)0x20;
	public const char SP_ZEN = (char)0x3000;
	public const char NBSP = (char)0x00a0;
}

}  // namespace md_svr
