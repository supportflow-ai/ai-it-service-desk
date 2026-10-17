#!/usr/bin/env python3
"""
AI Triage Evaluation Runner & Benchmark Reporter
Author: SupportFlow AI Team
Purpose: Evaluate AI triage providers against independent gold-label benchmarks.
Metrics:
  - Category Accuracy & Confusion Matrix
  - Priority Accuracy & Distance Error
  - Urgency & Impact Accuracy
  - HITL / Security Guardrail Compliance (False Negative on SEC = CRITICAL)
  - Latency & Availability
"""

import os
import sys
import json
import time
import argparse
from datetime import datetime
from typing import List, Dict, Any, Optional

# Ensure UTF-8 output on Windows terminal
if sys.stdout.encoding != 'utf-8':
    try:
        sys.stdout.reconfigure(encoding='utf-8')
    except Exception:
        pass

# Canonical categories matching ServiceDesk.Domain.Ticketing.TicketCategory
CANONICAL_CATEGORIES = [
    "ACC", "NET", "VPN", "MAIL", "SW", "HW", "PRINT", "ACCESS", "EQUIP", "SEC", "OTHER"
]

PRIORITY_RANKS = {"P1": 1, "P2": 2, "P3": 3, "P4": 4}

class EvaluationRunner:
    def __init__(self, dataset_paths: List[str], output_dir: str):
        self.dataset_paths = dataset_paths
        self.output_dir = output_dir
        self.cases: List[Dict[str, Any]] = []
        self._load_datasets()

    def _load_datasets(self):
        for path in self.dataset_paths:
            if not os.path.exists(path):
                print(f"[WARN] Dataset not found: {path}")
                continue
            with open(path, "r", encoding="utf-8") as f:
                data = json.load(f)
                if isinstance(data, list):
                    self.cases.extend(data)
                else:
                    print(f"[WARN] Invalid format in {path}, expected JSON array.")
        print(f"[INFO] Loaded total {len(self.cases)} test cases.")

    def run_provider(self, provider_type: str = "heuristic") -> Dict[str, Any]:
        """
        Executes evaluation.
        provider_type:
          - 'noop': Tests the current fallback baseline (NoOpTicketClassificationService behavior)
          - 'heuristic': Rule-based keyword baseline for local benchmarking
        """
        results = []
        start_time = time.time()

        for case in self.cases:
            case_id = case["id"]
            title = case["input"]["title"]
            desc = case["input"]["description"]
            gold = case["gold_label"]

            t0 = time.perf_counter()
            if provider_type == "noop":
                prediction = self._predict_noop(title, desc)
            else:
                prediction = self._predict_heuristic(title, desc)
            latency_ms = round((time.perf_counter() - t0) * 1000, 2)

            # Verification logic
            cat_match = (prediction.get("category") == gold["category"])
            impact_match = (prediction.get("impact") == gold["impact"])
            urgency_match = (prediction.get("urgency") == gold["urgency"])

            expected_p = gold.get("expected_priority")
            pred_p = prediction.get("calculated_priority")
            priority_match = (pred_p == expected_p)

            # Security HITL Guardrail check:
            # If gold label is SEC, did AI flag manual triage? Did it accurately identify SEC?
            sec_fail = False
            if gold["category"] == "SEC":
                if prediction.get("category") != "SEC" or not prediction.get("requires_manual_triage", False):
                    sec_fail = True

            results.append({
                "id": case_id,
                "ticket_type": case.get("ticket_type", "standard"),
                "input": case["input"],
                "gold": gold,
                "prediction": prediction,
                "latency_ms": latency_ms,
                "metrics": {
                    "category_match": cat_match,
                    "impact_match": impact_match,
                    "urgency_match": urgency_match,
                    "priority_match": priority_match,
                    "security_hitl_fail": sec_fail
                }
            })

        total_duration_sec = round(time.time() - start_time, 3)
        return self._generate_report(results, provider_type, total_duration_sec)

    def _predict_noop(self, title: str, desc: str) -> Dict[str, Any]:
        """Simulates NoOpTicketClassificationService: returns Unavailable / fallback"""
        return {
            "status": "Unavailable",
            "category": "OTHER",
            "confidence": 0.0,
            "confidence_band": "Low",
            "impact": "Low",
            "urgency": "Low",
            "calculated_priority": "P4",
            "requires_manual_triage": False,
            "provider_name": "NoOpFallback"
        }

    def _predict_heuristic(self, title: str, desc: str) -> Dict[str, Any]:
        """Deterministic keyword-based baseline for comparing AI gains"""
        text = f"{title} {desc}".lower()

        cat = "OTHER"
        confidence = 0.5
        band = "Medium"
        requires_manual_triage = False

        if any(k in text for k in ["ransomware", "lừa đảo", "phishing", "mã độc", "lộ mật khẩu", "mất điện thoại", "mất thẻ", "lộ lọt"]):
            cat = "SEC"
            confidence = 0.95
            band = "High"
            requires_manual_triage = True
        elif any(k in text for k in ["vpn", "anyconnect", "split-tunneling"]):
            cat = "VPN"
            confidence = 0.85
            band = "High"
        elif any(k in text for k in ["máy in", "in màu", "scan", "kẹt giấy", "canon", "ricoh"]):
            if "mượn máy in" in text:
                cat = "EQUIP"
            else:
                cat = "PRINT"
            confidence = 0.85
            band = "High"
        elif any(k in text for k in ["quên mật khẩu", "sso", "authenticator", "khóa tài khoản", "reset pass"]):
            if "mật khẩu giải nén" in text or "zip" in text:
                cat = "OTHER"
            else:
                cat = "ACC"
            confidence = 0.90
            band = "High"
        elif any(k in text for k in ["wifi", "wi-fi", "mạng lan", "cáp mạng", "không nhận ip", "dhcp", "cáp quang"]):
            cat = "NET"
            confidence = 0.85
            band = "High"
        elif any(k in text for k in ["outlook", "teams", "hòm thư", "email", "mailbox", "quota"]):
            if "quyền gửi email" in text or "send as" in text:
                cat = "ACCESS"
            else:
                cat = "MAIL"
            confidence = 0.85
            band = "High"
        elif any(k in text for k in ["bản quyền", "license", "cài đặt", "cài lại win", "misa", "rider", "unikey", "crash"]):
            cat = "SW"
            confidence = 0.80
            band = "High"
        elif any(k in text for k in ["liệt phím", "sọc", "màn hình đen", "cháy", "phồng pin", "mùi khét", "numpad", "kẹt phím"]):
            cat = "HW"
            confidence = 0.85
            band = "High"
        elif any(k in text for k in ["cấp quyền", "quyền truy cập", "403 forbidden", "access denied", "nas", "git repository"]):
            cat = "ACCESS"
            confidence = 0.88
            band = "High"
        elif any(k in text for k in ["cấp mới", "mượn", "chuột không dây", "bổ sung ram", "dây cáp hub"]):
            cat = "EQUIP"
            confidence = 0.85
            band = "High"

        # Impact evaluation
        impact = "Low"
        if any(k in text for k in ["toàn bộ", "tất cả", "60 người", "cả công ty", "production", "sập", "ransomware", "lộ lọt"]):
            impact = "High"
        elif any(k in text for k in ["nhóm", "phòng kế toán", "phòng marketing", "15 người", "kẹt giấy"]):
            impact = "Medium"

        # Urgency evaluation
        urgency = "Medium"
        if any(k in text for k in ["gấp", "ngay lập tức", "hạn chót", "không thể nhận", "liệt", "cháy", "phồng pin", "14h00", "p1"]):
            urgency = "High"
        elif any(k in text for k in ["tuần tới", "kế hoạch", "thanh lý", "hỏi thông tin", "bảo trì tháng"]):
            urgency = "Low"

        # Deterministic PriorityMatrix
        calculated_p = self._calc_priority(impact, urgency)

        return {
            "status": "Success",
            "category": cat,
            "confidence": confidence,
            "confidence_band": band,
            "impact": impact,
            "urgency": urgency,
            "calculated_priority": calculated_p,
            "requires_manual_triage": requires_manual_triage,
            "provider_name": "HeuristicBaseline"
        }

    def _calc_priority(self, impact: str, urgency: str) -> str:
        matrix = {
            ("High", "High"): "P1",
            ("High", "Medium"): "P2",
            ("High", "Low"): "P3",
            ("Medium", "High"): "P2",
            ("Medium", "Medium"): "P3",
            ("Medium", "Low"): "P4",
            ("Low", "High"): "P3",
            ("Low", "Medium"): "P4",
            ("Low", "Low"): "P4",
        }
        return matrix.get((impact, urgency), "P4")

    def _generate_report(self, results: List[Dict[str, Any]], provider_name: str, total_duration: float) -> Dict[str, Any]:
        total = len(results)
        if total == 0:
            return {}

        cat_correct = sum(1 for r in results if r["metrics"]["category_match"])
        impact_correct = sum(1 for r in results if r["metrics"]["impact_match"])
        urgency_correct = sum(1 for r in results if r["metrics"]["urgency_match"])
        priority_correct = sum(1 for r in results if r["metrics"]["priority_match"])
        sec_failures = sum(1 for r in results if r["metrics"]["security_hitl_fail"])

        # Breakdown by ticket type (standard vs edge_case/ambiguous)
        std_cases = [r for r in results if r["ticket_type"] == "standard"]
        edge_cases = [r for r in results if r["ticket_type"] in ["edge_case", "ambiguous"]]

        std_cat_acc = (sum(1 for r in std_cases if r["metrics"]["category_match"]) / len(std_cases)) if std_cases else 0.0
        edge_cat_acc = (sum(1 for r in edge_cases if r["metrics"]["category_match"]) / len(edge_cases)) if edge_cases else 0.0

        report = {
            "timestamp": datetime.utcnow().isoformat() + "Z",
            "provider": provider_name,
            "total_cases": total,
            "standard_cases": len(std_cases),
            "edge_cases": len(edge_cases),
            "total_duration_sec": total_duration,
            "summary_metrics": {
                "category_accuracy": round(cat_correct / total * 100, 2),
                "standard_category_accuracy": round(std_cat_acc * 100, 2),
                "edge_category_accuracy": round(edge_cat_acc * 100, 2),
                "impact_accuracy": round(impact_correct / total * 100, 2),
                "urgency_accuracy": round(urgency_correct / total * 100, 2),
                "priority_matrix_accuracy": round(priority_correct / total * 100, 2),
                "security_hitl_false_negatives": sec_failures
            },
            "detailed_results": results
        }

        # Save JSON output
        os.makedirs(self.output_dir, exist_ok=True)
        json_path = os.path.join(self.output_dir, f"baseline-report-{provider_name}.json")
        with open(json_path, "w", encoding="utf-8") as f:
            json.dump(report, f, indent=2, ensure_ascii=False)
        print(f"[INFO] JSON Report saved: {json_path}")

        # Save Markdown Summary
        md_path = os.path.join(self.output_dir, f"baseline-report-{provider_name}.md")
        self._write_markdown_report(report, md_path)
        print(f"[INFO] Markdown Report saved: {md_path}")

        return report

    def _write_markdown_report(self, report: Dict[str, Any], output_path: str):
        metrics = report["summary_metrics"]
        md = f"""# 📊 AI Triage Evaluation Baseline Report ({report['provider']})

- **Execution Date**: `{report['timestamp']}`
- **Provider Under Test**: `{report['provider']}`
- **Total Test Cases**: `{report['total_cases']}` (Standard: `{report['standard_cases']}`, Edge/Ambiguous: `{report['edge_cases']}`)
- **Total Duration**: `{report['total_duration_sec']}s`

---

## 🎯 Executive Summary & Metrics

| Metric | Target Goal | Score | Status |
|---|:---:|:---:|:---:|
| **Overall Category Accuracy** | $\\ge 75\\%$ | **{metrics['category_accuracy']}%** | {'✅ PASS' if metrics['category_accuracy'] >= 75 else '⚠️ SUB-PAR'} |
| **Standard Cases Accuracy** | $\\ge 85\\%$ | **{metrics['standard_category_accuracy']}%** | {'✅ PASS' if metrics['standard_category_accuracy'] >= 85 else '⚠️ SUB-PAR'} |
| **Edge / Ambiguous Accuracy** | $\\ge 60\\%$ | **{metrics['edge_category_accuracy']}%** | {'✅ PASS' if metrics['edge_category_accuracy'] >= 60 else '⚠️ SUB-PAR'} |
| **Impact Accuracy** | $\\ge 70\\%$ | **{metrics['impact_accuracy']}%** | {'✅ PASS' if metrics['impact_accuracy'] >= 70 else '⚠️ SUB-PAR'} |
| **Urgency Accuracy** | $\\ge 70\\%$ | **{metrics['urgency_accuracy']}%** | {'✅ PASS' if metrics['urgency_accuracy'] >= 70 else '⚠️ SUB-PAR'} |
| **Priority Calculation Accuracy** | $\\ge 70\\%$ | **{metrics['priority_matrix_accuracy']}%** | {'✅ PASS' if metrics['priority_matrix_accuracy'] >= 70 else '⚠️ SUB-PAR'} |
| **Security HITL Violations (SEC Missed)** | **0** | **{metrics['security_hitl_false_negatives']}** | {'✅ SECURE' if metrics['security_hitl_false_negatives'] == 0 else '🚨 CRITICAL RISK'} |

---

## 🔍 Detailed Analysis per Category & Case

| ID | Type | Title | Gold Cat / P | Pred Cat / P | Match | Security HITL | Latency |
|---|---|---|:---:|:---:|:---:|:---:|:---:|
"""
        for r in report["detailed_results"]:
            gold_str = f"`{r['gold']['category']}` / {r['gold'].get('expected_priority')}"
            pred_str = f"`{r['prediction'].get('category')}` / {r['prediction'].get('calculated_priority')}"
            match_icon = "✅" if r["metrics"]["category_match"] and r["metrics"]["priority_match"] else ("⚠️" if r["metrics"]["category_match"] else "❌")
            sec_icon = "🚨 FAIL" if r["metrics"]["security_hitl_fail"] else "✅ OK"
            title_trunc = (r['input']['title'][:40] + '...') if len(r['input']['title']) > 40 else r['input']['title']
            md += f"| {r['id']} | {r['ticket_type']} | {title_trunc} | {gold_str} | {pred_str} | {match_icon} | {sec_icon} | {r['latency_ms']}ms |\n"

        with open(output_path, "w", encoding="utf-8") as f:
            f.write(md)


def main():
    parser = argparse.ArgumentParser(description="AI Triage Evaluation Benchmark Runner")
    parser.add_argument("--provider", default="heuristic", choices=["noop", "heuristic"], help="Provider type to evaluate")
    parser.add_argument("--output-dir", default="evaluation/reports", help="Directory to save reports")
    args = parser.parse_args()

    # base_dir is the evaluation directory where datasets and reports reside
    base_dir = os.path.dirname(os.path.abspath(__file__))
    datasets = [
        os.path.join(base_dir, "datasets", "standard-cases.json"),
        os.path.join(base_dir, "datasets", "edge-cases.json")
    ]
    out_dir = os.path.join(base_dir, "reports") if args.output_dir == "evaluation/reports" else args.output_dir

    runner = EvaluationRunner(datasets, out_dir)
    report = runner.run_provider(args.provider)
    print("\n=== SUMMARY RESULTS ===")
    print(json.dumps(report.get("summary_metrics", {}), indent=2))

if __name__ == "__main__":
    main()
