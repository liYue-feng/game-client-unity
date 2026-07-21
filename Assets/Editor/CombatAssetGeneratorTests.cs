using System;
using System.IO;
using NUnit.Framework;

namespace Game.Editor
{
    public sealed class CombatAssetGeneratorTests
    {
        [Test]
        public void WriteIfMissing_DoesNotOverwriteExistingPngOrWav()
        {
            var sentinel = new byte[] { 0x53, 0x45, 0x4E, 0x54 };
            var replacement = new byte[] { 0x4F, 0x56, 0x45, 0x52 };

            foreach (var extension in new[] { ".png", ".wav" })
            {
                var path = Path.Combine(
                    Path.GetTempPath(),
                    $"combat-asset-generator-{Guid.NewGuid():N}{extension}");

                try
                {
                    File.WriteAllBytes(path, sentinel);

                    Assert.That(CombatAssetGenerator.WriteIfMissing(path, replacement), Is.False);
                    Assert.That(File.ReadAllBytes(path), Is.EqualTo(sentinel));
                }
                finally
                {
                    if (File.Exists(path))
                    {
                        File.Delete(path);
                    }
                }
            }
        }
    }
}
