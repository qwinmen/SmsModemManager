using System;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;

/*
 * Взято
 * https://www.cyberforum.ru/csharp-net/thread789315.html
 */
namespace SmsConsoleTest
{
	public class Sms: IDisposable
	{
		private readonly SerialPort _port;

		public Sms(string portName)
		{
			try
			{
				_port = new SerialPort(portName)
					{
						PortName = portName,
						WriteTimeout = 500,
						ReadTimeout = 500,
						BaudRate = 9600,
						Parity = Parity.None,
						DataBits = 8,
						StopBits = StopBits.One,
						Handshake = Handshake.RequestToSend,
						DtrEnable = true,
						RtsEnable = true,
						NewLine = Environment.NewLine
					};

				_port.Open();
				_port.DataReceived += SerialPortDataReceived;
			}
			catch (Exception ex)
			{
				throw new ApplicationException(ex.Message);
			}
		}

		public bool AtOk { get; set; }
		public string MessageLog { get; private set; }

		public void Dispose()
		{
			_port.Close();
			_port.Dispose();
		}

		[STAThread]
		private void SerialPortDataReceived(object sender, SerialDataReceivedEventArgs e)
		{
			//"\r\n+CMGS: 26\r\n\r\nOK\r\n"
			if (!_port.IsOpen)
				return;

			string tmp = _port.ReadExisting();
			tmp = tmp.Replace("\r\n", "").Replace(" ", "");
			if (tmp.Length < 5)
				return;

			if (tmp.Substring(0, 5) == "+CMGS")
			{
				AtOk = true;
			}

			MessageLog += tmp + Environment.NewLine;
		}

		public void SendSms(string number, string text)
		{
			var revNumber = ReverseNumber(number);
			var revText = StringToUcs2(text);

			if (string.IsNullOrEmpty(revNumber))
			{
				MessageLog += $"Номер {number} преобразован неверно" + Environment.NewLine;
				return;
			}

			if (string.IsNullOrEmpty(revText))
			{
				MessageLog += $"Строка: [{text}] преобразована неверно" + Environment.NewLine;
				return;
			}

			//Формируем пакет sms
			var packSms = new StringBuilder();

			packSms.Append("00"); //СМС-Центр    00  В начале указывается номер СМС-цента для отсылки, он просто хранится в телефоне и используется по умолчанию
			packSms.Append("11"); //SMS-SUBMIT   11  Тип сообщения - 1 (SMS-SUBMIT). Далее указывается о наличии байта таймаута (+10)
			packSms.Append("00"); //TP-Message-Reference 00  "Номер" сообщения TP-Message-Reference. Оставляем 0 - не наше дело
			packSms.Append("0B91" + revNumber); //Получатель:
			//0B    Длина номера получателя, 11 цифр, включая код страны "7"
			//91    Международный формат
			//Номер который сформирует reverseNumber
			packSms.Append("00"); //TP-PID   00  Тип протокола. Оставляем 0.
			packSms.Append("08"); //TP-DCS   08  Кодировка сообщения. 00 = 7bit, 04 - 8bit, 08 - 16 bit UCS-2. Для передачи русского языка выбираем 08.
			packSms.Append("AA"); //TP-Validity-Period   AA  Таймаут для сообщения (наличие указывалось в SMS-SUBMIT). AA - 4 дня. 
			packSms.Append(ByteSize(revText)); //TP-User-Data-Length Длина сообщения в байтах.
			packSms.Append(revText); //TP-User-Data Сообщение сформирует reverseText
			var resultSms = packSms.ToString();
			_port.WriteLine("AT+CMGF=0"); //Режим PDU-0, TEXT-1
			Thread.Sleep(500);
			_port.WriteLine("AT+CMGS=" + ByteSizeRes(resultSms)); //Кол-во байт-1
			Thread.Sleep(500);
			_port.WriteLine(resultSms + (char)(26)); //Посылаем СМС и код клавиш ctrl+z
		}

		static string ReverseNumber(string numb)
		{
			var result = "";
			var ind = 0;
			var tmp = "";
			try
			{
				for (var i = 0; i < numb.Length; i++)
				{
					if (ind > 1)
					{
						ind = 1;

						result += ReverseDigit(tmp);
						if (String.IsNullOrEmpty(result))
						{
							return "";
						}

						tmp = numb[i].ToString();
						if (i == numb.Length - 1)
						{
							result += ReverseDigit(tmp);
						}
					}
					else
					{
						tmp += numb[i];
						ind++;
					}
				}
			}
			catch /*(Exception ex)*/
			{
				//Тут надо отправить уведомление отделу ИТ
				result = "";
			}

			return result;
		}

		static string ReverseDigit(string twoDigit)
		{
			if (twoDigit.Length == 2) //Если 2 цифры, меняем местами
			{
				var tmp = twoDigit[0].ToString();
				twoDigit = twoDigit[1] + tmp;
				return twoDigit;
			}

			if (twoDigit.Length == 1) //Если 1, то добавляем к ней F
			{
				return "F" + twoDigit;
			}

			return "";
		}

		static string StringToUcs2(string text)
		{
			string result;
			try
			{
				var enc = Encoding.BigEndianUnicode;
				var intermediate = enc.GetBytes(text);
				var sb = new StringBuilder(intermediate.Length * 2);
				foreach (var b in intermediate)
				{
					sb.Append(b.ToString("X2"));
				}

				result = sb.ToString();
			}
			catch /*(Exception ex)*/
			{
				//Тут надо отправить уведомление отделу ИТ
				result = "";
			}

			return result;
		}

		static string ByteSize(string str)
		{
			var size = str.Length / 2;
			var ost = str.Length % 2;
			if (ost != 0) size += 1;
			return size.ToString("X2");
		}

		static string ByteSizeRes(string str)
		{
			var size = str.Length / 2;
			var ost = str.Length % 2;
			if (ost != 0) size += 1;
			return (size - 1).ToString();
		}
	}
}
