#!/usr/bin/env python3
import argparse
import json
import sqlite3
from pathlib import Path


DEFAULT_TEMPLATE = Path(__file__).with_name("admin_rank_templates.json")
DEFAULT_DATABASE = Path("preferences.db")


def quote_identifier(name: str) -> str:
    return '"' + name.replace('"', '""') + '"'


def load_templates(path: Path) -> list[dict[str, object]]:
    with path.open("r", encoding="utf-8") as file:
        data = json.load(file)

    ranks = data.get("ranks")
    if not isinstance(ranks, list):
        raise ValueError("Template file must contain a 'ranks' array")

    seen_names: set[str] = set()
    for rank in ranks:
        if not isinstance(rank, dict):
            raise ValueError("Each rank template must be an object")

        name = rank.get("name")
        flags = rank.get("flags")
        if not isinstance(name, str) or not name:
            raise ValueError("Each rank template must have a non-empty string name")
        if name in seen_names:
            raise ValueError(f"Duplicate rank template: {name}")
        seen_names.add(name)

        if not isinstance(flags, list) or not all(isinstance(flag, str) and flag for flag in flags):
            raise ValueError(f"Rank {name} must have a non-empty string flags array")
        if len(flags) != len(set(flags)):
            raise ValueError(f"Rank {name} has duplicate flags")

    return ranks


def require_tables(cursor: sqlite3.Cursor) -> None:
    tables = {
        row[0]
        for row in cursor.execute(
            "select name from sqlite_master where type = 'table'"
        )
    }
    missing = {"admin_rank", "admin_rank_flag"} - tables
    if missing:
        raise RuntimeError(f"Database is missing required tables: {', '.join(sorted(missing))}")


def rank_id_for_name(cursor: sqlite3.Cursor, name: str) -> int | None:
    rows = list(
        cursor.execute(
            "select admin_rank_id from admin_rank where name = ? order by admin_rank_id",
            (name,),
        )
    )
    if len(rows) > 1:
        ids = ", ".join(str(row[0]) for row in rows)
        raise RuntimeError(f"Rank name {name!r} is duplicated in admin_rank: {ids}")
    if not rows:
        return None
    return int(rows[0][0])


def existing_flags(cursor: sqlite3.Cursor, rank_id: int) -> set[str]:
    return {
        row[0]
        for row in cursor.execute(
            "select flag from admin_rank_flag where admin_rank_id = ?",
            (rank_id,),
        )
    }


def seed(database: Path, templates: list[dict[str, object]], dry_run: bool) -> None:
    connection = sqlite3.connect(database)
    try:
        cursor = connection.cursor()
        require_tables(cursor)
        cursor.execute("begin immediate")
        try:
            for rank in templates:
                name = str(rank["name"])
                flags = [str(flag) for flag in rank["flags"]]

                rank_id = rank_id_for_name(cursor, name)
                if rank_id is None:
                    print(f"create rank: {name}")
                    if not dry_run:
                        cursor.execute("insert into admin_rank (name) values (?)", (name,))
                        rank_id = int(cursor.lastrowid)
                    else:
                        rank_id = -1
                else:
                    print(f"update rank: {name}")

                current_flags = set() if rank_id == -1 else existing_flags(cursor, rank_id)
                target_flags = set(flags)
                add_flags = sorted(target_flags - current_flags)
                remove_flags = sorted(current_flags - target_flags)

                if add_flags:
                    print(f"  add flags: {', '.join(add_flags)}")
                if remove_flags:
                    print(f"  remove flags: {', '.join(remove_flags)}")

                if not dry_run:
                    cursor.execute(
                        "delete from admin_rank_flag where admin_rank_id = ?",
                        (rank_id,),
                    )
                    cursor.executemany(
                        "insert into admin_rank_flag (admin_rank_id, flag) values (?, ?)",
                        [(rank_id, flag) for flag in flags],
                    )

            if dry_run:
                cursor.execute("rollback")
            else:
                cursor.execute("commit")
        except Exception:
            cursor.execute("rollback")
            raise
    finally:
        connection.close()


def print_ranks(database: Path) -> None:
    connection = sqlite3.connect(database)
    try:
        cursor = connection.cursor()
        require_tables(cursor)
        ranks = list(cursor.execute(
            "select admin_rank_id, name from admin_rank order by admin_rank_id"
        ))
        for rank_id, name in ranks:
            flags = [
                row[0]
                for row in cursor.execute(
                    "select flag from admin_rank_flag where admin_rank_id = ? order by flag",
                    (rank_id,),
                )
            ]
            print(f"{rank_id}\t{name}\t{','.join(flags)}")
    finally:
        connection.close()


def main() -> None:
    parser = argparse.ArgumentParser(
        description="Seed Tiny-Station admin rank templates into a SQLite preferences database."
    )
    parser.add_argument(
        "--database",
        type=Path,
        default=DEFAULT_DATABASE,
        help="Path to preferences.db. Defaults to ./preferences.db.",
    )
    parser.add_argument(
        "--templates",
        type=Path,
        default=DEFAULT_TEMPLATE,
        help="Path to admin_rank_templates.json.",
    )
    parser.add_argument(
        "--dry-run",
        action="store_true",
        help="Validate and print changes without writing.",
    )
    parser.add_argument(
        "--list",
        action="store_true",
        help="Print ranks after seeding.",
    )
    args = parser.parse_args()

    templates = load_templates(args.templates)
    seed(args.database, templates, args.dry_run)

    if args.list:
        print_ranks(args.database)


if __name__ == "__main__":
    main()
