﻿using OpenNefia.Core.ContentPack;
using OpenNefia.Core.Log;
using OpenNefia.Core.Utility;

namespace OpenNefia.Core.SaveGames
{
    internal class SaveGameDirProvider : ISaveGameDirProvider
    {
        /// <summary>
        /// This directory holds the save's temporary data that should be wiped
        /// when loading.
        /// </summary>
        private readonly IWritableDirProvider _tempDir;

        /// <summary>
        /// This directory holds the comitted save data.
        /// </summary>
        private readonly IWritableDirProvider _committedDir;

        /// <summary>
        /// Paths that will be deleted when the save is committed.
        /// </summary>
        private readonly HashSet<ResourcePath> _pendingDeletions = new();

        public string? RootDir => _tempDir.RootDir;

        public SaveGameDirProvider(IWritableDirProvider temp, IWritableDirProvider committed)
        {
            _committedDir = committed;
            _tempDir = temp;
        }

        /// <inheritdoc/>
        public IWritableDirProvider GetChild(ResourcePath path)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public void CreateDirectory(ResourcePath path)
        {
            _tempDir.CreateDirectory(path);
        }

        /// <inheritdoc/>
        public void Delete(ResourcePath path)
        {
            if (_tempDir.Exists(path))
            {
                _tempDir.Delete(path);
            }
            _pendingDeletions.Add(path);
        }

        /// <inheritdoc/>
        public bool Exists(ResourcePath path)
        {
            if (!path.IsRooted)
            {
                throw new ArgumentException("Path must be rooted.");
            }

            foreach (var deletion in _pendingDeletions)
            {
                if (path == deletion || path.TryRelativeTo(deletion, out _))
                    return false;
            }

            return _committedDir.Exists(path) || _tempDir.Exists(path);
        }

        /// <inheritdoc/>
        public bool IsDirectory(ResourcePath path)
        {
            if (!path.IsRooted)
            {
                throw new ArgumentException("Path must be rooted.");
            }

            foreach (var deletion in _pendingDeletions)
            {
                if (path == deletion || path.TryRelativeTo(deletion, out _))
                    return false;
            }

            return _committedDir.Exists(path) || _tempDir.Exists(path);
        }

        /// <inheritdoc/>
        public DateTime GetLastWriteTime(ResourcePath path)
        {
            if (_tempDir.Exists(path))
                return _tempDir.GetLastWriteTime(path);

            return _committedDir.GetLastWriteTime(path);
        }

        /// <inheritdoc/>
        public (IEnumerable<ResourcePath> files, IEnumerable<ResourcePath> directories) Find(string pattern, bool recursive = true)
        {
            var (comittedFiles, committedDirs) = _committedDir.Find(pattern, recursive);
            var (tempFiles, tempDirs) = _tempDir.Find(pattern, recursive);

            var allFiles = tempFiles.Union(comittedFiles)!;
            var allDirs = tempDirs.Union(committedDirs)!;

            return (allFiles, allDirs);
        }

        /// <inheritdoc/>
        public Stream Open(ResourcePath path, FileMode fileMode, FileAccess access, FileShare share)
        {
            if (!_tempDir.Exists(path) && _committedDir.Exists(path))
            {
                Logger.DebugS("save.writer", $"Copying file as uncommitted: {path}");
                CopyBetweenDirProviders(_committedDir, _tempDir, FileMode.CreateNew, path);
                RemoveDeletion(path);
            }

            return _tempDir.Open(path, fileMode, access, share);
        }

        private void CopyBetweenDirProviders(IWritableDirProvider from, IWritableDirProvider to, FileMode fileMode, ResourcePath path)
        {
            var dir = path.Directory.ToRootedPath();

            if (dir != ResourcePath.Root)
            {
                to.CreateDirectory(dir);
                from.CreateDirectory(dir);
            }

            using var reader = from.OpenRead(path);
            using var writer = to.Open(path, fileMode, FileAccess.ReadWrite, FileShare.None);
            reader.CopyTo(writer);
        }

        /// <inheritdoc/>
        public void Rename(ResourcePath oldPath, ResourcePath newPath)
        {
            if (!_tempDir.Exists(oldPath) && _committedDir.Exists(oldPath))
            {
                Logger.DebugS("save.writer", $"Renaming file as uncommitted: {oldPath}");
                CopyBetweenDirProviders(_committedDir, _tempDir, FileMode.CreateNew, oldPath);
            }

            _tempDir.Rename(oldPath, newPath);
            _pendingDeletions.Add(oldPath);
            RemoveDeletion(newPath);
        }

        private void RemoveDeletion(ResourcePath path)
        {
            foreach (var deletion in _pendingDeletions.ToList())
            {
                if (path == deletion || path.TryRelativeTo(deletion, out _))
                    _pendingDeletions.Remove(deletion);
            }
        }

        /// <inheritdoc/>
        public void ClearTemp()
        {
            Logger.DebugS("save.writer", "Clearing temporary directory");

            var (files, dirs) = _tempDir.Find("*", recursive: false);
        
            foreach (var file in files)
            {
                _tempDir.Delete(file);
            }

            foreach (var dir in dirs)
            {
                _tempDir.Delete(dir);
            }

            _pendingDeletions.Clear();
        }

        /// <inheritdoc/>
        public void Commit()
        {
            var (files, dirs) = _tempDir.Find("*", recursive: true);

            foreach (var file in files)
            {
                CopyBetweenDirProviders(_tempDir, _committedDir, FileMode.OpenOrCreate, file.ToRootedPath());
            }

            foreach (var dir in dirs)
            {
                _committedDir.CreateDirectory(dir);
            }

            foreach (var deletion in _pendingDeletions)
            {
                _committedDir.Delete(deletion);
            }

            ClearTemp();
        }
    }
}