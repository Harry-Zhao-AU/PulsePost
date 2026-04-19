"""Handles APPROVE / REJECT / EDIT comments on draft issues."""

import json
import os
import re
import sys
from datetime import datetime
import requests
from github import Github
from openai import AzureOpenAI

REPO_NAME = "Harry-Zhao-AU/PulsePost"
BLOG_REPO_NAME = "Harry-Zhao-AU/harry-zhao-au.github.io"
TELEGRAM_BOT_TOKEN = os.environ["TELEGRAM_BOT_TOKEN"]
TELEGRAM_CHAT_ID = os.environ["TELEGRAM_CHAT_ID"]

client = AzureOpenAI(
    azure_endpoint=os.environ["AZURE_OPENAI_ENDPOINT"],
    api_key=os.environ["AZURE_OPENAI_KEY"],
    api_version="2024-08-01-preview",
)
DEPLOYMENT = os.environ["AZURE_OPENAI_DEPLOYMENT"]


def send_telegram(message: str):
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


def parse_issue_body(body: str) -> dict:
    article_match = re.search(r"## Blog Article\n\n(.*?)\n\n---", body, re.DOTALL)
    thread_match = re.search(r"## X Thread\n\n(.*?)\n\n---", body, re.DOTALL)
    image_match = re.search(r"## Image Prompt\n\n```\n(.*?)\n```", body, re.DOTALL)
    title_match = re.search(r"\[DRAFT\] (.+)", body)

    return {
        "article": article_match.group(1).strip() if article_match else "",
        "x_thread_raw": thread_match.group(1).strip() if thread_match else "",
        "image_prompt": image_match.group(1).strip() if image_match else "",
        "title": title_match.group(1).strip() if title_match else "Article",
    }


def handle_approve(issue, draft: dict):
    gh = Github(os.environ["GH_PAT"])
    blog_repo = gh.get_repo(BLOG_REPO_NAME)

    slug = re.sub(r"[^a-z0-9]+", "-", draft["title"].lower()).strip("-")
    date_str = datetime.utcnow().strftime("%Y-%m-%d")
    filename = f"_posts/{date_str}-{slug}.md"

    front_matter = f"""---
layout: post
title: "{draft['title']}"
date: {date_str}
categories: [AI, Engineering]
---

"""
    content = front_matter + draft["article"]

    blog_repo.create_file(
        path=filename,
        message=f"Add article: {draft['title']}",
        content=content,
        branch="main",
    )

    pr = blog_repo.create_pull(
        title=f"Article: {draft['title']}",
        body=f"## New Article\n\n**Image Prompt:**\n```\n{draft['image_prompt']}\n```\n\nGenerate image, upload to `assets/images/`, update front matter, then merge.",
        head="main",
        base="main",
    )

    issue.edit(state="closed", labels=["approved"])

    send_telegram(
        f"✅ *Approved!*\n\n"
        f"Blog PR created: {pr.html_url}\n\n"
        f"*X Thread — copy and post manually:*\n\n"
        f"{draft['x_thread_raw']}\n\n"
        f"*Image Prompt:*\n`{draft['image_prompt']}`"
    )


def handle_reject(issue):
    issue.edit(state="closed", labels=["rejected"])
    send_telegram("❌ *Draft rejected.* Discarded.")


def handle_edit(issue, draft: dict, feedback: str):
    prompt = f"""Rewrite this blog article based on the following feedback.

Original article:
{draft['article']}

Feedback: {feedback}

Return the improved article in Markdown format only. Keep the same title and structure unless feedback says otherwise."""

    response = client.chat.completions.create(
        model=DEPLOYMENT,
        messages=[{"role": "user", "content": prompt}],
        temperature=0.7,
        max_tokens=2000,
    )
    revised_article = response.choices[0].message.content

    thread_prompt = f"""Rewrite this X thread to match the revised article.

Revised article:
{revised_article}

Feedback that was applied: {feedback}

Rules: 6-8 tweets, max 280 chars each, direct and technical tone.
Return JSON only: {{"tweets": ["tweet 1", ...]}}"""

    thread_response = client.chat.completions.create(
        model=DEPLOYMENT,
        messages=[{"role": "user", "content": thread_prompt}],
        response_format={"type": "json_object"},
        temperature=0.7,
    )
    revised_tweets = json.loads(thread_response.choices[0].message.content)["tweets"]
    x_thread_formatted = "\n".join(
        f"**Tweet {i+1}:** {tweet}" for i, tweet in enumerate(revised_tweets)
    )

    new_body = issue.body.replace(
        draft["article"], revised_article
    ).replace(
        draft["x_thread_raw"], x_thread_formatted
    )
    issue.edit(body=new_body)

    preview = "\n".join(f"• {t}" for t in revised_tweets[:2])
    send_telegram(
        f"✏️ *Draft Revised*\n\n"
        f"Feedback applied: _{feedback}_\n\n"
        f"*X Thread Preview:*\n{preview}\n\n"
        f"[Review updated issue]({issue.html_url})\n\n"
        f"Reply: `APPROVE` / `REJECT` / `EDIT <more feedback>`"
    )


if __name__ == "__main__":
    event = json.loads(sys.stdin.read())

    issue_number = event["issue"]["number"]
    comment_body = event["comment"]["body"].strip()
    commenter = event["comment"]["user"]["login"]

    # Only accept commands from the repo owner
    if commenter != "Harry-Zhao-AU":
        print(f"Ignoring comment from {commenter}", file=sys.stderr)
        sys.exit(0)

    gh = Github(os.environ["GH_PAT"])
    repo = gh.get_repo(REPO_NAME)
    issue = repo.get_issue(issue_number)

    # Only handle pending-approval issues
    label_names = [l.name for l in issue.labels]
    if "pending-approval" not in label_names:
        print("Issue is not pending approval, skipping.", file=sys.stderr)
        sys.exit(0)

    draft = parse_issue_body(issue.title + "\n\n" + issue.body)
    draft["title"] = issue.title.replace("[DRAFT] ", "")

    if comment_body.upper() == "APPROVE":
        print("Handling APPROVE...", file=sys.stderr)
        handle_approve(issue, draft)

    elif comment_body.upper() == "REJECT":
        print("Handling REJECT...", file=sys.stderr)
        handle_reject(issue)

    elif comment_body.upper().startswith("EDIT"):
        feedback = comment_body[4:].strip()
        if not feedback:
            send_telegram("⚠️ EDIT requires feedback. Example: `EDIT make it more technical`")
            sys.exit(1)
        print(f"Handling EDIT: {feedback}", file=sys.stderr)
        handle_edit(issue, draft, feedback)

    else:
        print(f"Unknown command: {comment_body}", file=sys.stderr)
