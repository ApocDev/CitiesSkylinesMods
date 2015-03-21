using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ApocDev.CitySkylines.Mod.Chirpy
{
	internal class CustomMessage : MessageBase
	{
		private readonly string _sender, _message;

		public CustomMessage(string sender, string message)
		{
			_sender = sender;
			_message = message;
		}

		public override string GetSenderName()
		{
			return _sender;
		}

		public override string GetText()
		{
			return _message;
		}
	}
}
