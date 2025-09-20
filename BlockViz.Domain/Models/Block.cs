using System;

namespace BlockViz.Domain.Models
{
    /// <summary>
    /// 엑셀/CSV에서 읽어오는 블록 데이터의 표준 모델.
    /// 매핑(컬럼명 호환)은 ExcelImportService 내부 ClassMap에서 처리합니다.
    /// </summary>
    public class Block
    {
        // ── 식별/표시 ──────────────────────────────────────────────
        public string Name { get; set; } = string.Empty;   // BlockName | name
        public int BlockID { get; set; }                   // BlockIDNumber | id

        // ── 일정 ─────────────────────────────────────────────────
        public DateTime Start { get; set; }                // StartDate | startdate (yyyyMMdd)
        public DateTime End { get; set; }                  // EndDate   | enddate   (yyyyMMdd)
        public DateTime? Due { get; set; }                 // DueDate (있으면 사용, 없으면 null)
        public int ProcessingTime { get; set; }            // ProcessingTime (옵션)

        // ── 배치/공간 ────────────────────────────────────────────
        public int DeployWorkplace { get; set; }           // DeployWorkplace | workspace

        /// <summary>가로(X방향) 길이 — 기존 Length, 신규 w</summary>
        public double Length { get; set; }

        /// <summary>세로(Y방향) 길이 — 기존 Breadth, 신규 h</summary>
        public double Breadth { get; set; }

        /// <summary>높이(Z방향) — 기존 Depth, 신규도 Depth 그대로</summary>
        public double Height { get; set; }

        public int NumberOfBlocks { get; set; }            // 옵션: 기존 헤더가 있으면 채워짐

        /// <summary>블록 중심의 X좌표 — 기존 BlockxCoord, 신규 center_x</summary>
        public double X { get; set; }

        /// <summary>블록 중심의 Y좌표 — 기존 BlockyCoord, 신규 center_y</summary>
        public double Y { get; set; }

        /// <summary>방향(미사용 가능) — 기존 BlockDirection (신규 o는 무시)</summary>
        public int Direction { get; set; }
    }
}
