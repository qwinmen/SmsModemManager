// See https://aka.ms/new-console-template for more information

using System;

namespace SmsConsoleTest
{
	internal class Program
	{
		static void Main(string[] args)
		{
			Console.WriteLine("Hello World lol!");
			string resultMsg;

			using (Sms sms = new Sms("com9"))
			{
				//if (sms.AtOk)
				{
					sms.SendSms("79151234567", "test");
					//Console.WriteLine(msg);
					resultMsg = sms.MessageLog;
				}
			}
			Console.WriteLine(resultMsg);
			Console.ReadLine();
		}
	}
}
