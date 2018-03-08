using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;

namespace MusicBeePlugin.Domain
{
    public sealed class ConnectStream : Stream
    {
        public readonly string ContentType;
        private HttpWebResponse _webResponse;
        private Stream _responseStream;

        public ConnectStream(Uri uri)
        {
            var httpRequest = (HttpWebRequest) WebRequest.Create(uri);
            httpRequest.Accept = "*/*";
            httpRequest.Method = "GET";
            httpRequest.Timeout = 30000;
            _webResponse = (HttpWebResponse) httpRequest.GetResponse();
            ContentType = _webResponse.ContentType;
            Length = _webResponse.ContentLength;
            _responseStream = _webResponse.GetResponseStream();
            CanRead = true;
            CanSeek = false;
            CanWrite = false;
        }

        protected override void Dispose(bool disposing)
        {
            CloseResponses();
        }

        private void CloseResponses()
        {
            if (_responseStream != null)
            {
                _responseStream.Flush();
                _responseStream.Close();
                _responseStream = null;
            }
            if (_webResponse != null)
            {
                _webResponse.Close();
                _webResponse = null;
            }
        }

        /// <inheritdoc />
        /// <summary>When overridden in a derived class, clears all buffers for this stream and causes any buffered data to be written to the underlying device.</summary>
        /// <exception cref="T:System.IO.IOException">An I/O error occurs. </exception>
        /// <filterpriority>2</filterpriority>
        public override void Flush()
        {
            _responseStream.Flush();
        }

        /// <inheritdoc />
        /// <summary>When overridden in a derived class, sets the position within the current stream.</summary>
        /// <returns>The new position within the current stream.</returns>
        /// <param name="offset">A byte offset relative to the <paramref name="origin" /> parameter. </param>
        /// <param name="origin">A value of type <see cref="T:System.IO.SeekOrigin" /> indicating the reference point used to obtain the new position. </param>
        /// <exception cref="T:System.IO.IOException">An I/O error occurs. </exception>
        /// <exception cref="T:System.NotSupportedException">The stream does not support seeking, such as if the stream is constructed from a pipe or console output. </exception>
        /// <exception cref="T:System.ObjectDisposedException">Methods were called after the stream was closed. </exception>
        /// <filterpriority>1</filterpriority>
        public override long Seek(long offset, SeekOrigin origin)
        {
            return _responseStream.Seek(offset, origin);
        }

        /// <inheritdoc />
        /// <summary>When overridden in a derived class, sets the length of the current stream.</summary>
        /// <param name="value">The desired length of the current stream in bytes. </param>
        /// <exception cref="T:System.IO.IOException">An I/O error occurs. </exception>
        /// <exception cref="T:System.NotSupportedException">The stream does not support both writing and seeking, such as if the stream is constructed from a pipe or console output. </exception>
        /// <exception cref="T:System.ObjectDisposedException">Methods were called after the stream was closed. </exception>
        /// <filterpriority>2</filterpriority>
        public override void SetLength(long value)
        {
            _responseStream.SetLength(value);
        }

        /// <inheritdoc />
        /// <summary>When overridden in a derived class, reads a sequence of bytes from the current stream and advances the position within the stream by the number of bytes read.</summary>
        /// <returns>The total number of bytes read into the buffer. This can be less than the number of bytes requested if that many bytes are not currently available, or zero (0) if the end of the stream has been reached.</returns>
        /// <param name="buffer">An array of bytes. When this method returns, the buffer contains the specified byte array with the values between <paramref name="offset" /> and (<paramref name="offset" /> + <paramref name="count" /> - 1) replaced by the bytes read from the current source. </param>
        /// <param name="offset">The zero-based byte offset in <paramref name="buffer" /> at which to begin storing the data read from the current stream. </param>
        /// <param name="count">The maximum number of bytes to be read from the current stream. </param>
        /// <exception cref="T:System.ArgumentException">The sum of <paramref name="offset" /> and <paramref name="count" /> is larger than the buffer length. </exception>
        /// <exception cref="T:System.ArgumentNullException">
        /// <paramref name="buffer" /> is null. </exception>
        /// <exception cref="T:System.ArgumentOutOfRangeException">
        /// <paramref name="offset" /> or <paramref name="count" /> is negative. </exception>
        /// <exception cref="T:System.IO.IOException">An I/O error occurs. </exception>
        /// <exception cref="T:System.NotSupportedException">The stream does not support reading. </exception>
        /// <exception cref="T:System.ObjectDisposedException">Methods were called after the stream was closed. </exception>
        /// <filterpriority>1</filterpriority>
        public override int Read(byte[] buffer, int offset, int count)
        {
            return _responseStream.Read(buffer, offset, count);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            _responseStream.Write(buffer, offset, count);
        }

        public override bool CanRead { get; }
        public override bool CanSeek { get; }
        public override bool CanWrite { get; }

        public override long Length { get; }

        public override long Position
        {
            get { return _responseStream.Position; }
            set { }
        }
    }
}
