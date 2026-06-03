# Tiny-Station Tools

## Admin Rank Templates

`admin_rank_templates.json` stores the default Tiny-Station admin rank templates:

- `Стажёр`
- `Админ`
- `Гейм-мастер`
- `Основатель`

`seed_admin_ranks.py` applies those templates to a SQLite `preferences.db`.
It creates missing ranks, updates flags for existing ranks with matching names,
and does not touch assigned admins, bans, or players.

Example:

```sh
python3 Tools/tinystation/seed_admin_ranks.py \
  --database /var/lib/ww-station/preferences.db \
  --list
```

Dry run:

```sh
python3 Tools/tinystation/seed_admin_ranks.py \
  --database /var/lib/ww-station/preferences.db \
  --dry-run
```
