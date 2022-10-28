﻿using UnityEngine;
using System.Net.Sockets;
using System.Text;

namespace Incredulous.Twitch
{

    internal static class NetworkStreamExtensionMethods
    {
        public static void WriteLine(this NetworkStream stream, string output)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(output);
            stream.Write(bytes, 0, bytes.Length);
            stream.WriteByte((byte)'\r');
            stream.WriteByte((byte)'\n');
            stream.Flush();
        }
    }

}