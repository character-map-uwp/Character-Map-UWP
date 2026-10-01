using System;
using System.Globalization;
using System.Numerics;

namespace CharacterMap.Core;

public interface IPathReceiver
{
    void BeginFigure(Vector2 startPoint);
    void AddLine(Vector2 endPoint);
    void AddQuadraticBezier(Vector2 controlPoint, Vector2 endPoint);
    void AddCubicBezier(Vector2 controlPoint1, Vector2 controlPoint2, Vector2 endPoint);
    void AddArc(Vector2 endPoint, float rx, float ry, float rotationAngle, bool isClockwise, bool isLargeArc);
    void EndFigure(bool isClosed);
}

public static class SvgPathParser
{
    public static void Parse(string d, IPathReceiver builder)
    {
        if (string.IsNullOrWhiteSpace(d))
            return;

        int pos = 0;
        char prevCommand = ' ';
        Vector2 currentPoint = Vector2.Zero;
        Vector2 lastControlPoint = Vector2.Zero;
        bool figureStarted = false;

        void EnsureFigureStarted()
        {
            if (!figureStarted)
            {
                builder.BeginFigure(currentPoint);
                figureStarted = true;
            }
        }

        while (pos < d.Length)
        {
            SkipWhitespace(d, ref pos);
            if (pos >= d.Length)
                break;

            char c = d[pos];
            char command;
            bool implicitCommand = false;

            if (char.IsLetter(c))
            {
                command = c;
                pos++;
            }
            else
            {
                if (prevCommand == ' ')
                    break;
                
                command = prevCommand;
                implicitCommand = true;
                if (command == 'm') command = 'l';
                else if (command == 'M') command = 'L';
            }

            bool isRelative = char.IsLower(command);
            char cmdUpper = char.ToUpperInvariant(command);

            switch (cmdUpper)
            {
                case 'M':
                {
                    float x = ReadFloat(d, ref pos);
                    float y = ReadFloat(d, ref pos);
                    
                    if (isRelative && prevCommand != ' ')
                    {
                        x += currentPoint.X;
                        y += currentPoint.Y;
                    }

                    if (figureStarted)
                    {
                        builder.EndFigure(false);
                        figureStarted = false;
                    }

                    currentPoint = new Vector2(x, y);
                    lastControlPoint = currentPoint;
                    
                    builder.BeginFigure(currentPoint);
                    figureStarted = true;
                    break;
                }
                case 'Z':
                {
                    if (figureStarted)
                    {
                        builder.EndFigure(true);
                        figureStarted = false;
                    }
                    break;
                }
                case 'L':
                {
                    float x = ReadFloat(d, ref pos);
                    float y = ReadFloat(d, ref pos);
                    
                    if (isRelative)
                    {
                        x += currentPoint.X;
                        y += currentPoint.Y;
                    }

                    EnsureFigureStarted();
                    Vector2 nextPoint = new Vector2(x, y);
                    builder.AddLine(nextPoint);
                    currentPoint = nextPoint;
                    lastControlPoint = currentPoint;
                    break;
                }
                case 'H':
                {
                    float x = ReadFloat(d, ref pos);
                    if (isRelative)
                        x += currentPoint.X;

                    EnsureFigureStarted();
                    Vector2 nextPoint = new Vector2(x, currentPoint.Y);
                    builder.AddLine(nextPoint);
                    currentPoint = nextPoint;
                    lastControlPoint = currentPoint;
                    break;
                }
                case 'V':
                {
                    float y = ReadFloat(d, ref pos);
                    if (isRelative)
                        y += currentPoint.Y;

                    EnsureFigureStarted();
                    Vector2 nextPoint = new Vector2(currentPoint.X, y);
                    builder.AddLine(nextPoint);
                    currentPoint = nextPoint;
                    lastControlPoint = currentPoint;
                    break;
                }
                case 'C':
                {
                    float x1 = ReadFloat(d, ref pos);
                    float y1 = ReadFloat(d, ref pos);
                    float x2 = ReadFloat(d, ref pos);
                    float y2 = ReadFloat(d, ref pos);
                    float x = ReadFloat(d, ref pos);
                    float y = ReadFloat(d, ref pos);

                    if (isRelative)
                    {
                        x1 += currentPoint.X; y1 += currentPoint.Y;
                        x2 += currentPoint.X; y2 += currentPoint.Y;
                        x += currentPoint.X; y += currentPoint.Y;
                    }

                    EnsureFigureStarted();
                    Vector2 nextPoint = new Vector2(x, y);
                    Vector2 cp2 = new Vector2(x2, y2);
                    builder.AddCubicBezier(new Vector2(x1, y1), cp2, nextPoint);
                    currentPoint = nextPoint;
                    lastControlPoint = cp2;
                    break;
                }
                case 'S':
                {
                    float x2 = ReadFloat(d, ref pos);
                    float y2 = ReadFloat(d, ref pos);
                    float x = ReadFloat(d, ref pos);
                    float y = ReadFloat(d, ref pos);

                    if (isRelative)
                    {
                        x2 += currentPoint.X; y2 += currentPoint.Y;
                        x += currentPoint.X; y += currentPoint.Y;
                    }

                    float x1 = currentPoint.X;
                    float y1 = currentPoint.Y;

                    if (prevCommand == 'c' || prevCommand == 'C' || prevCommand == 's' || prevCommand == 'S')
                    {
                        x1 = currentPoint.X + (currentPoint.X - lastControlPoint.X);
                        y1 = currentPoint.Y + (currentPoint.Y - lastControlPoint.Y);
                    }

                    EnsureFigureStarted();
                    Vector2 nextPoint = new Vector2(x, y);
                    Vector2 cp2 = new Vector2(x2, y2);
                    builder.AddCubicBezier(new Vector2(x1, y1), cp2, nextPoint);
                    currentPoint = nextPoint;
                    lastControlPoint = cp2;
                    break;
                }
                case 'Q':
                {
                    float x1 = ReadFloat(d, ref pos);
                    float y1 = ReadFloat(d, ref pos);
                    float x = ReadFloat(d, ref pos);
                    float y = ReadFloat(d, ref pos);

                    if (isRelative)
                    {
                        x1 += currentPoint.X; y1 += currentPoint.Y;
                        x += currentPoint.X; y += currentPoint.Y;
                    }

                    EnsureFigureStarted();
                    Vector2 nextPoint = new Vector2(x, y);
                    Vector2 cp = new Vector2(x1, y1);
                    builder.AddQuadraticBezier(cp, nextPoint);
                    currentPoint = nextPoint;
                    lastControlPoint = cp;
                    break;
                }
                case 'T':
                {
                    float x = ReadFloat(d, ref pos);
                    float y = ReadFloat(d, ref pos);

                    if (isRelative)
                    {
                        x += currentPoint.X; y += currentPoint.Y;
                    }

                    float x1 = currentPoint.X;
                    float y1 = currentPoint.Y;

                    if (prevCommand == 'q' || prevCommand == 'Q' || prevCommand == 't' || prevCommand == 'T')
                    {
                        x1 = currentPoint.X + (currentPoint.X - lastControlPoint.X);
                        y1 = currentPoint.Y + (currentPoint.Y - lastControlPoint.Y);
                    }

                    EnsureFigureStarted();
                    Vector2 nextPoint = new Vector2(x, y);
                    Vector2 cp = new Vector2(x1, y1);
                    builder.AddQuadraticBezier(cp, nextPoint);
                    currentPoint = nextPoint;
                    lastControlPoint = cp;
                    break;
                }
                case 'A':
                {
                    float rx = ReadFloat(d, ref pos);
                    float ry = ReadFloat(d, ref pos);
                    float angle = ReadFloat(d, ref pos);
                    bool largeArc = ReadBool(d, ref pos);
                    bool sweep = ReadBool(d, ref pos);
                    float x = ReadFloat(d, ref pos);
                    float y = ReadFloat(d, ref pos);

                    if (isRelative)
                    {
                        x += currentPoint.X; y += currentPoint.Y;
                    }

                    EnsureFigureStarted();
                    Vector2 nextPoint = new Vector2(x, y);
                    float radians = angle * (float)Math.PI / 180.0f;

                    builder.AddArc(nextPoint, rx, ry, radians, sweep, largeArc);
                    currentPoint = nextPoint;
                    lastControlPoint = currentPoint;
                    break;
                }
                default:
                    break;
            }

            if (!implicitCommand || cmdUpper == 'M')
                prevCommand = command;
            else if (implicitCommand && cmdUpper == 'M')
                prevCommand = isRelative ? 'l' : 'L';
        }

        if (figureStarted)
        {
            builder.EndFigure(false);
        }
    }

    private static void SkipWhitespace(string s, ref int pos)
    {
        while (pos < s.Length && (char.IsWhiteSpace(s[pos]) || s[pos] == ','))
        {
            pos++;
        }
    }

    private static float ReadFloat(string s, ref int pos)
    {
        SkipWhitespace(s, ref pos);
        
        int start = pos;
        if (pos < s.Length && (s[pos] == '+' || s[pos] == '-'))
            pos++;
            
        bool hasDot = false;
        while (pos < s.Length)
        {
            char c = s[pos];
            if (char.IsDigit(c))
                pos++;
            else if (c == '.' && !hasDot)
            {
                hasDot = true;
                pos++;
            }
            else if (c == 'e' || c == 'E')
            {
                pos++;
                if (pos < s.Length && (s[pos] == '+' || s[pos] == '-'))
                    pos++;
            }
            else
            {
                break;
            }
        }

        if (start == pos)
            return 0f;

        string numStr = s.Substring(start, pos - start);
        float.TryParse(numStr, NumberStyles.Float, CultureInfo.InvariantCulture, out float result);
        return result;
    }

    private static bool ReadBool(string s, ref int pos)
    {
        SkipWhitespace(s, ref pos);
        if (pos < s.Length)
        {
            char c = s[pos];
            if (c == '0')
            {
                pos++;
                return false;
            }
            else if (c == '1')
            {
                pos++;
                return true;
            }
        }
        
        return ReadFloat(s, ref pos) > 0;
    }
}
