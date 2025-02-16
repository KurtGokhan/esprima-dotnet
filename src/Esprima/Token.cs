﻿using System.Configuration;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Esprima.Ast;

namespace Esprima;

public enum TokenType : byte
{
    Unknown,
    BooleanLiteral,
    EOF,
    Identifier,
    Keyword,
    NullLiteral,
    NumericLiteral,
    Punctuator,
    StringLiteral,
    RegularExpression,
    Template,
    BigIntLiteral,

    Extension = byte.MaxValue
}

[StructLayout(LayoutKind.Auto)]
public readonly record struct Token
{
    internal abstract record ValueHolder(object? Value);

    internal readonly object? _value;

    internal Token(
        TokenType type,
        object? value,
        int start,
        int end,
        int lineNumber,
        int lineStart,
        bool octal = false)
    {
        Type = type;
        _value = value;

        Octal = octal;
        Start = start;
        End = end;
        LineNumber = lineNumber;
        LineStart = lineStart;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Token Create(TokenType type, object? value, int start, int end, int lineNumber, int lineStart)
    {
        return new Token(type, value, start, end, lineNumber, lineStart);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Token CreateStringLiteral(string str, bool octal, int start, int end, int lineNumber, int lineStart)
    {
        return new Token(TokenType.StringLiteral, str, start, end, lineNumber, lineStart, octal);
    }

    private sealed record RegexHolder(object? Value, RegexValue RegexValue) : ValueHolder(Value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Token CreateRegexLiteral(Regex? value, RegexValue regexValue, int start, int end, int lineNumber, int lineStart)
    {
        return new Token(TokenType.RegularExpression, new RegexHolder(value, regexValue), start, end, lineNumber, lineStart);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Token CreateNumericLiteral(double value, bool octal, int start, int end, int lineNumber, int lineStart)
    {
        return new Token(TokenType.NumericLiteral, value, start, end, lineNumber, lineStart, octal: octal);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Token CreateBigIntLiteral(BigInteger value, int start, int end, int lineNumber, int lineStart)
    {
        return new Token(TokenType.BigIntLiteral, value, start, end, lineNumber, lineStart);
    }

    internal static Token CreateEof(int index, int lineNumber, int lineStart)
    {
        return new Token(TokenType.EOF, value: null, start: index, end: index, lineNumber, lineStart);
    }

    internal static Token CreatePunctuator(string str, int start, int end, int lineNumber, int lineStart)
    {
        return new Token(TokenType.Punctuator, str, start, end, lineNumber, lineStart);
    }

    private sealed record TemplateHolder(object? Value, string RawTemplate, char NotEscapeSequenceHead, bool Head, bool Tail) : ValueHolder(Value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Token CreateTemplate(
        string? cooked,
        string rawTemplate,
        bool head,
        bool tail,
        char notEscapeSequenceHead,
        int start,
        int end,
        int lineNumber,
        int lineStart)
    {
        var value = new TemplateHolder(cooked, rawTemplate, notEscapeSequenceHead, head, tail);
        return new Token(TokenType.Template, value, start, end, lineNumber, lineStart);
    }

    public readonly TokenType Type;
    public readonly bool Octal;

    public readonly int Start; // Range[0]
    public readonly int End; // Range[1]
    public readonly int LineNumber;
    public readonly int LineStart;

    public object? Value
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            // NOTE: This condition must not be inverted, otherwise the runtime (.NET 6) fail to inline the accessor correctly.
            return Type is not (TokenType.RegularExpression or TokenType.Template or TokenType.Extension)
                ? _value
                : GetValueFromHolder(in this);

            [MethodImpl(MethodImplOptions.NoInlining)]
            static object? GetValueFromHolder(in Token token) => ((ValueHolder) token._value!).Value;
        }
    }

    internal char NotEscapeSequenceHead => Type == TokenType.Template ? ((TemplateHolder) _value!).NotEscapeSequenceHead : char.MinValue;
    public bool Head => Type == TokenType.Template && ((TemplateHolder) _value!).Head;
    public bool Tail => Type == TokenType.Template && ((TemplateHolder) _value!).Tail;

    public string? RawTemplate
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Type == TokenType.Template ? ((TemplateHolder) _value!).RawTemplate : null;
    }

    public RegexValue? RegexValue
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Type == TokenType.RegularExpression ? ((RegexHolder) _value!).RegexValue : null;
    }

    internal Token ChangeType(TokenType newType)
    {
        return new Token(newType, _value, Start, End, LineNumber, LineStart, Octal);
    }
}
