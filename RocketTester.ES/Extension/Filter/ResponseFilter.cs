using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace RocketTester.ES
{
    public class ResponseFilter : Stream
    {
        #region properties
        Stream responseStream;
        long position;
        #endregion

        #region constructor
        public ResponseFilter(Stream inputStream)
        {
            responseStream = inputStream;
            Body = new StringBuilder();
        }
        #endregion

        #region implemented abstract members
        public StringBuilder Body { get; set; }

        public override bool CanRead
        {
            get { return true; }
        }

        public override bool CanSeek
        {
            get { return true; }
        }

        public override bool CanWrite
        {
            get { return true; }
        }

        public override void Close()
        {
            responseStream.Close();
        }

        public override void Flush()
        {
            responseStream.Flush();
        }

        public override long Length
        {
            get { return 0; }
        }

        public override long Position
        {
            get { return position; }
            set { position = value; }
        }

        public override long Seek(long offset, System.IO.SeekOrigin direction)
        {
            return responseStream.Seek(offset, direction);
        }

        public override void SetLength(long length)
        {
            responseStream.SetLength(length);
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return responseStream.Read(buffer, offset, count);
        }
        #endregion

        #region write method
        public override void Write(byte[] buffer, int offset, int count)
        {
            string sBuffer = System.Text.UTF8Encoding.UTF8.GetString(buffer, offset, count);
            /*
            //得到非法词汇列表，这个可以在数据库或Web.Config中读取出来 
            string pattern = @"(非法词汇1|非法词汇2|非法词汇3)";
            string[] s = pattern.Split(new string[] { "|" }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string s1 in s)
            {
                sBuffer = sBuffer.Replace(s1, "**");
            }
            //*/
            Body.Append(sBuffer);
            byte[] data = System.Text.UTF8Encoding.UTF8.GetBytes(sBuffer);
            responseStream.Write(data, 0, data.Length);
        }
        #endregion
    }
}
