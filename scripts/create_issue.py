"""Creates a GitHub Issue with the draft and sends a Telegram notification."""

import json
import os
import sys
import requests
from github import Github

REPO_NAME = "Harry-Zhao-AU/PulsePost"
TELEGRAM_BOT_TOKEN = os.environ["TELEGRAM_BOT_TOKEN"]
TELEGRAM_CHAT_ID = os.environ["TELEGRAM_CHAT_ID"]


def create_github_issue(draft: dict) -> tuple[int, str]:
    gh = Github(os.environ["GH_PAT"])
    repo = gh.get_repo(REPO_NAME)

    topic = draft["topic"]
    x_thread_formatted = "\n".join(
        f"**Tweet {i+1}:** {tweet}" for i, tweet in enumerate(draft["x_thread"])
    )

    body = f"""## Blog Article

{draft['article']}

---

## X Thread

{x_thread_formatted}

---

## Image Prompt

```
{draft['image_prompt']}
```

---

## Sources Used

{chr(10).join(f"- [{s['name']}]({s['url']}) — {s['from']}" for s in topic.get('sources', []))}

**Why now:** {topic['why_now']}

---
*Reply with `APPROVE`, `REJECT`, or `EDIT <your feedback>` to proceed.*"""

    issue = repo.create_issue(
        title=f"[DRAFT] {topic['title']}",
        body=body,
        labels=["pending-approval"],
    )

    return issue.number, issue.html_url


def send_telegram(draft: dict, issue_number: int, issue_url: str):
    topic = draft["topic"]
    preview_tweets = "\n".join(
        f"• {t}" for t in draft["x_thread"][:2]
    )

    message = (
        f"📝 *New Draft Ready*\n\n"
        f"*Topic:* {topic['title']}\n\n"
        f"*Sources:*\n" + "\n".join(f"• [{s['name']}]({s['url']})" for s in topic.get('sources', [])) + "\n\n"
        f"*X Thread Preview:*\n{preview_tweets}\n\n"
        f"*Image Prompt:*\n`{draft['image_prompt']}`\n\n"
        f"[Review Issue #{issue_number}]({issue_url})\n\n"
        f"Reply on GitHub: `APPROVE` / `REJECT` / `EDIT <feedback>`"
    )

    requests.post(
        f"https://api.telegram.org/bot{TELEGRAM_BOT_TOKEN}/sendMessage",
        json={
            "chat_id": TELEGRAM_CHAT_ID,
            "text": message,
            "parse_mode": "Markdown",
            "disable_web_page_preview": True,
        },
        timeout=10,
    ).raise_for_status()


if __name__ == "__main__":
    draft = json.loads(sys.stdin.read())

    print("Creating GitHub issue...", file=sys.stderr)
    issue_number, issue_url = create_github_issue(draft)
    print(f"Issue #{issue_number} created: {issue_url}", file=sys.stderr)

    print("Sending Telegram notification...", file=sys.stderr)
    send_telegram(draft, issue_number, issue_url)
    print("Telegram sent.", file=sys.stderr)
