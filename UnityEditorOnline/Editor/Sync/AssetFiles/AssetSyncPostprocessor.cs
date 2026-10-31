using UnityEditor;

namespace UnityEditorOnline.Editor.Sync.AssetFiles
{
    public class AssetSyncPostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            foreach (string path in imported)
                if (AssetSync.IsSyncable(path)) AssetSync.SendLocal(path);
        }
    }
}