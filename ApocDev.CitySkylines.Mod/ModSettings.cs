using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

namespace ApocDev.CitySkylines.Mod
{
	public class ModSettings
	{
		public const string SettingsPath = "ApocDevModSettings.xml";


		[XmlIgnore]
		private static ModSettings _instance;


#if !MOD_FIRESPREAD
		public bool ModifyCapacities { get; set; }
		public int PassengerTrainCapacity { get; set; }
		public int MetroCapacity { get; set; }
		public int BusCapacity { get; set; }
		public int PassengerPlaneCapacity { get; set; }
		public int PassengerShipCapacity { get; set; }
#endif
		public bool EnableFireSpread { get; set; }
		public float FireSpreadModifier { get; set; }
		public float BaseFireSpreadChance { get; set; }
		public float NoWaterFireSpreadAdditional { get; set; }
		public float UneducatedFireSpreadAdditional { get; set; }

		public static void Load()
		{
			if (_instance != null)
				return;

			XmlSerializer serializer = new XmlSerializer(typeof(ModSettings));

			if (!File.Exists(SettingsPath))
				using (var fs = File.Create(SettingsPath))
				{
					using (var writer = XmlWriter.Create(fs))
					{
						writer.WriteComment("BaseFireSpreadChance - float - The base chance (in %) that a building can have fire spread to it");
						writer.WriteComment("NoWaterFireSpreadAdditional - float - An additional chance (in %) that a building can have fire spread to it, if there is no water hookups to the building. This is added on top of the base chance.");
						writer.WriteComment("UneducatedFireSpreadAdditional - float - An additional chance (in %) that a building can have fire spread to it, if there are no educated workers building. This is added on top of the base chance.");
						writer.WriteComment("FireSpreadModifier - float - A modifier for the overall chance for fire to be spread to a building. The internal 'random' generator creates a number between 0.0 and 1.0 so this value should modify the total chance to within that range.");
						serializer.Serialize(fs, new ModSettings());
					}
				}

			using (var fs = File.OpenRead(SettingsPath))
				_instance = serializer.Deserialize(fs) as ModSettings;
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
			set { _instance = value; }
		}

		public ModSettings()
		{
#if !MOD_FIRESPREAD
			// Default: 30
			BusCapacity = 90;
			// Default: 30
			PassengerTrainCapacity = 480;
			// Default: 100
			PassengerShipCapacity = 100;
			// Default: 30
			PassengerPlaneCapacity = 350;
			// Default: 30
			MetroCapacity = 360;
#endif


			FireSpreadModifier = 0.00725f;
			BaseFireSpreadChance = 2.5f;
			NoWaterFireSpreadAdditional = 7f;
			UneducatedFireSpreadAdditional = 1f;
		}
	}
}
