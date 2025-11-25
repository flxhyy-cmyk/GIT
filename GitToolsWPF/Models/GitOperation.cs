namespace GitToolsWPF.Models
{
    public enum GitOperationType
    {
        ViewStatus,
        InitialPush,
        Update,
        Release,
        ViewRepository,
        CleanKeepHistory,
        CleanDeleteHistory
    }

    public class GitOperationResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = "";
        public string Output { get; set; } = "";
    }

    public class VersionInfo
    {
        public string TagName { get; set; } = "";
        public string Message { get; set; } = "";
        public string Date { get; set; } = "";
        public string CommitHash { get; set; } = "";
    }

    public class CommitInfo
    {
        public string Hash { get; set; } = "";
        public string ShortHash { get; set; } = "";
        public string Message { get; set; } = "";
        public string Author { get; set; } = "";
        public string Date { get; set; } = "";
        public bool IsCurrent { get; set; } = false;
        public string GraphSymbols { get; set; } = "";  // 图形符号 (*, |, /, \)
        public string Branches { get; set; } = "";      // 分支标签 (HEAD, main, origin/main)
        public bool IsHead { get; set; } = false;       // 是否是 HEAD 位置
        public bool IsLocalBranch { get; set; } = false;    // 是否有本地分支
        public bool IsRemoteBranch { get; set; } = false;   // 是否有远程分支
        public bool IsMainLine { get; set; } = false;   // 是否是主线（没有分支符号 |, /, \）
        
        // 版本编号相关
        public string VersionNumber { get; set; } = "";     // 版本编号 (1, 2, 3, 3A, 3B 等)
        public int MainLineNumber { get; set; } = 0;        // 主线编号 (1, 2, 3...)
        public string BranchSuffix { get; set; } = "";      // 分支后缀 (A, B, C...)
        public bool IsBranchCommit { get; set; } = false;   // 是否是分支提交
        public string ParentHash { get; set; } = "";        // 父提交哈希
    }
    
    public class BranchInfo
    {
        public string Name { get; set; } = "";
        public bool IsCurrent { get; set; } = false;
        public bool IsRemote { get; set; } = false;
    }
}
