using System;
using System.IO;
using System.Text;

namespace dnGREP.Common
{
    public class EolReader(TextReader reader) : IDisposable
    {
        private readonly char[] charBuffer = new char[1024];
        private int charPos;
        private int charLen;

        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }

        private int ReadBuffer()
        {
            charLen = 0;
            charPos = 0;

            charLen = reader.ReadBlock(charBuffer, 0, 1024);

            return charLen;
        }

        public bool EndOfStream
        {
            get
            {
                if (reader == null)
                    return true;

                if (charPos < charLen)
                    return false;

                int numRead = ReadBuffer();
                return numRead == 0;
            }
        }

        public string? ReadLine()
        {
            if (reader == null)
                return null;

            if (charPos == charLen)
            {
                if (ReadBuffer() == 0) return null;
            }

            StringBuilder? sb = null;
            do
            {
                int i = charPos;
                do
                {
                    char ch = charBuffer[i];
                    i++;
                    // Note the following common line feed chars:
                    // \n - UNIX   \r\n - DOS   \r - Mac

                    if (ch == '\r' || ch == '\n')
                    {
                        // Include the \n of a \r\n pair in the single string allocation.
                        // If the \r is the last char in the buffer, the next char is only
                        // available after the buffer is refilled; that case is rare.
                        int end = i;
                        bool crAtBufferEnd = false;
                        if (ch == '\r')
                        {
                            if (i < charLen)
                            {
                                if (charBuffer[i] == '\n')
                                    end++;
                            }
                            else
                            {
                                crAtBufferEnd = true;
                            }
                        }

                        string s;
                        if (sb != null)
                        {
                            sb.Append(charBuffer, charPos, end - charPos);
                            s = sb.ToString();
                        }
                        else
                        {
                            s = new string(charBuffer, charPos, end - charPos);
                        }

                        charPos = end;
                        if (crAtBufferEnd && ReadBuffer() > 0 && charBuffer[charPos] == '\n')
                        {
                            charPos++;
                            s += '\n';
                        }
                        return s;
                    }

                } while (i < charLen);

                i = charLen - charPos;

                sb ??= new StringBuilder(i + 80);
                sb.Append(charBuffer, charPos, i);

            } while (ReadBuffer() > 0);

            return sb.ToString();
        }
    }
}