namespace UnityEditorOnline.Editor.Protocols
{
    public static class PacketType
    {
        public const string SnapshotBegin = "snapshot_begin";
        public const string Create = "create";
        public const string Destroy = "destroy";
        public const string Move = "move";
        public const string Set = "set";
        public const string Components = "components";
        public const string Asset = "asset";
        public const string Selection = "selection";
        public const string Camera = "camera";
    }
}