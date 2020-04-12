namespace MkvTracksSwapper
{
    public enum TrackType
    {
        Video,
        Audio,
        Subtitles
    }

    public class Track
    {
        public int TrackNumber { get; set; }
        public string UID { get; set; } = string.Empty;
        public string Language { get; set; } = "eng";
        public TrackType Type { get; set; }

        public bool Equals(Track other)
        {
            if (ReferenceEquals(other, null))
            {
                return false;
            }

            if (ReferenceEquals(other, this))
            {
                return true;
            }

            return UID == other.UID;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as Track);
        }

        public static bool operator ==(Track a, Track b)
        {
            return Equals(a, b);
        }

        public static bool operator !=(Track a, Track b)
        {
            return !Equals(a, b);
        }

        public override int GetHashCode()
        {
            if (int.TryParse(UID, out int hashCode))
                return hashCode;
            return 0;
        }
    }
}