"""Fetches trending AI topics from HN, company blogs, Reddit, and arXiv."""

import json
import os
import sys
import time
import feedparser
import requests

HN_KEYWORDS = ["ai", "llm", "agent", "mcp", "rag", "vector", "gpt", "claude",
               "neural", "transformer", "embedding", "copilot", "openai", "anthropic",
               "gemini", "mistral", "llama", "deepmind", "diffusion"]

COMPANY_FEEDS = [
    {"name": "Anthropic",   "url": "https://www.anthropic.com/rss.xml"},
    {"name": "OpenAI",      "url": "https://openai.com/blog/rss.xml"},
    {"name": "Google DeepMind", "url": "https://deepmind.google/blog/rss.xml"},
    {"name": "Meta AI",     "url": "https://ai.meta.com/blog/rss/"},
    {"name": "Microsoft AI","url": "https://blogs.microsoft.com/ai/feed/"},
]

REDDIT_SUBREDDITS = ["MachineLearning", "LocalLLaMA"]

GH_HEADERS = {
    "Authorization": f"Bearer {os.environ['GH_PAT']}",
    "Accept": "application/vnd.github+json",
}


def fetch_hacker_news():
    top_ids = requests.get(
        "https://hacker-news.firebaseio.com/v0/topstories.json", timeout=10
    ).json()

    results = []
    for story_id in top_ids[:200]:
        story = requests.get(
            f"https://hacker-news.firebaseio.com/v0/item/{story_id}.json", timeout=10
        ).json()
        title = (story.get("title") or "").lower()
        if any(kw in title for kw in HN_KEYWORDS):
            results.append({
                "source": "hackernews",
                "title": story.get("title"),
                "url": story.get("url", ""),
                "score": story.get("score", 0),
            })
        if len(results) >= 10:
            break
        time.sleep(0.05)

    return sorted(results, key=lambda x: x["score"], reverse=True)[:5]


def fetch_company_blogs():
    results = []
    for company in COMPANY_FEEDS:
        try:
            feed = feedparser.parse(company["url"])
            for entry in feed.entries[:3]:
                results.append({
                    "source": "company_blog",
                    "company": company["name"],
                    "title": entry.get("title", ""),
                    "url": entry.get("link", ""),
                    "summary": entry.get("summary", "")[:300],
                    "published": entry.get("published", ""),
                })
        except Exception as e:
            print(f"Failed to fetch {company['name']}: {e}", file=sys.stderr)

    return results


def fetch_reddit():
    results = []
    headers = {"User-Agent": "PulsePost/1.0"}
    for subreddit in REDDIT_SUBREDDITS:
        try:
            response = requests.get(
                f"https://www.reddit.com/r/{subreddit}/hot.json?limit=5",
                headers=headers,
                timeout=10,
            ).json()
            for post in response["data"]["children"]:
                data = post["data"]
                results.append({
                    "source": "reddit",
                    "subreddit": subreddit,
                    "title": data["title"],
                    "url": f"https://reddit.com{data['permalink']}",
                    "score": data["score"],
                    "num_comments": data["num_comments"],
                })
        except Exception as e:
            print(f"Failed to fetch r/{subreddit}: {e}", file=sys.stderr)

    return sorted(results, key=lambda x: x["score"], reverse=True)[:5]


def fetch_arxiv():
    feed = feedparser.parse("https://rss.arxiv.org/rss/cs.AI")
    return [
        {
            "source": "arxiv",
            "title": entry.title,
            "url": entry.get("link", ""),
            "description": entry.get("summary", "")[:300],
        }
        for entry in feed.entries[:8]
    ]


def fetch_existing_posts():
    response = requests.get(
        "https://api.github.com/repos/Harry-Zhao-AU/harry-zhao-au.github.io/contents/_posts",
        headers=GH_HEADERS,
        timeout=10,
    )
    if response.status_code != 200:
        return []
    return [f["name"].replace(".md", "") for f in response.json() if isinstance(f, dict)]


if __name__ == "__main__":
    print("Fetching Hacker News...", file=sys.stderr)
    hn = fetch_hacker_news()

    print("Fetching company blogs (Anthropic, OpenAI, Google, Meta, Microsoft)...", file=sys.stderr)
    company = fetch_company_blogs()

    print("Fetching Reddit (r/MachineLearning, r/LocalLLaMA)...", file=sys.stderr)
    reddit = fetch_reddit()

    print("Fetching arXiv...", file=sys.stderr)
    arxiv = fetch_arxiv()

    print("Fetching existing posts...", file=sys.stderr)
    existing = fetch_existing_posts()

    output = {
        "hackernews": hn,
        "company_blogs": company,
        "reddit": reddit,
        "arxiv": arxiv,
        "existing_posts": existing,
    }

    print(json.dumps(output, indent=2))
