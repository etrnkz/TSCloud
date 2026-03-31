#!/usr/bin/env python3
"""
Optional integration check against the live Telegram Bot API.

Do not put real tokens in this file. GitHub and scanners will flag them.

Usage:
  export TSCLOUD_BOT_TOKEN="your_token_from_botfather"
  python test_real_telegram.py

Revoke any token that was ever committed: https://t.me/BotFather -> /revoke
"""

from __future__ import annotations

import os
import sys
import urllib.error
import urllib.request


def main() -> int:
    token = os.environ.get("TSCLOUD_BOT_TOKEN", "").strip()
    if not token:
        print(
            "Skip: set TSCLOUD_BOT_TOKEN to run (never commit tokens).",
            file=sys.stderr,
        )
        return 0

    url = f"https://api.telegram.org/bot{token}/getMe"
    try:
        with urllib.request.urlopen(url, timeout=30) as resp:
            body = resp.read().decode("utf-8", errors="replace")
    except urllib.error.URLError as e:
        print(f"Request failed: {e}", file=sys.stderr)
        return 1

    print(body)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
