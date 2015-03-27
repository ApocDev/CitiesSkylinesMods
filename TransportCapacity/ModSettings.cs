using System.IO;
using System.Xml.Serialization;

namespace TransportCapacity
{
	public class ModSettings
	{
		public const string SettingsPath = "TransportCapacityModSettings.xml";


		[XmlIgnore]
		private static ModSettings _instance;


		public bool ModifyTransportCapacities { get; set; }
		public int PassengerTrainCapacity { get; set; }
		public int MetroCapacity { get; set; }
		public int BusCapacity { get; set; }
		public int PassengerPlaneCapacity { get; set; }
		public int PassengerShipCapacity { get; set; }

		public void Save()
		{
			XmlSerializer serializer = new XmlSerializer(typeof(ModSettings));
			using (var fs = File.Create(SettingsPath))
				serializer.Serialize(fs, this);
		}

		public static void Load()
		{
			if (_instance != null)
				return;

			XmlSerializer serializer = new XmlSerializer(typeof(ModSettings));

			// If a settings file doesn't exist, have the serializer generate us one.
			if (!File.Exists(SettingsPath))
			{
				_instance = new ModSettings();
				_instance.Save();
			}
			else
			{
				using (var fs = File.OpenRead(SettingsPath))
					_instance = serializer.Deserialize(fs) as ModSettings;
			}
		}

		[XmlIgnore]
		public static ModSettings Instance
		{
			get
			{
				if (_instance == null)
					Load();
				return _instance;
			}
		}

		public ModSettings()
		{
			// Default: 30
			BusCapacity = 90;
			// Default: 30
			PassengerTrainCapacity = 90;
			// Default: 100
			PassengerShipCapacity = 450;
			// Default: 30
			PassengerPlaneCapacity = 350;
			// Default: 30
			MetroCapacity = 90;
		}
	}
}
