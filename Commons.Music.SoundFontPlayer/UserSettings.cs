using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Commons.Music.SoundFontPlayer
{
	[DataContract]
	public class UserSettings
	{
		[DataMember] public IList<string> SoundFontPaths { get; set; } = new List<string> ();
	}
}