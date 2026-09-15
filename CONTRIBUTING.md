# How work is organised in this repository

One rule: **`main` always works.** It builds with no warnings, all tests pass,
and the `.exe` it produces is one you could hand to someone on site today.

Everything else follows from that.

---

## The cycle

Each piece of work gets its own branch, so that later you can look at the history
and see *what changed for which reason* rather than one long line of commits.

```bash
# 1. start from an up-to-date main
git switch main
git pull

# 2. branch before writing any code
git switch -c feature/pdf-export

# 3. work; commit as often as makes sense
dotnet test                       # must pass before every commit
git add -A
git commit

# 4. fold it back into main, keeping the branch visible in the graph
git switch main
git pull
git merge --no-ff feature/pdf-export
dotnet test                       # must pass after the merge too
git push
```

### Why `--no-ff` matters

Without it, Git "fast-forwards": your commits are appended to `main` and the fact
that they belonged together disappears. With `--no-ff` you get a merge commit, so
the feature shows up as its own strand:

```
*   9f2c1ab  Merge branch 'feature/pdf-export'
|\
| * 4d81e07  Write the metryki straight to PDF
| * 7c0b93a  Add a PDF page renderer
|/
*   7227733  Restructure on MVP, test the whole app, rewrite the README
```

That is the "clear path of what changed" — you can see at a glance that two
commits made up the PDF export, and `git revert -m 1 9f2c1ab` undoes the whole
feature in one step if it turns out to be wrong.

### Seeing the path

```bash
git log --graph --oneline --decorate --all     # the whole picture
git log --oneline main..feature/pdf-export     # just what this branch adds
git diff main...feature/pdf-export             # the code it changes
```

---

## Branch names

`<type>/<short-dash-separated-description>`, all lowercase.

| Prefix | For | Example |
|---|---|---|
| `feature/` | something the user can now do that they could not before | `feature/work-journal` |
| `fix/` | a defect in behaviour that already shipped | `fix/csv-decimal-comma` |
| `refactor/` | the code changes shape, the behaviour does not | `refactor/mvp-architecture` |
| `docs/` | documentation only | `docs/branching-workflow` |
| `chore/` | build, tooling, dependencies | `chore/bump-closedxml` |

Keep them short and about the outcome, not the method: `feature/pdf-export`, not
`feature/add-new-pdf-writing-class`.

---

## What is already in the history

The work done before this convention existed has been labelled after the fact,
so the branches line up with the features:

| Branch | Commit | What it brought |
|---|---|---|
| `feature/poc-generator` | `d389f4b` | reading the schedule, writing the metryki workbook, the offline `.exe` |
| `feature/work-journal` | `b1bed4c` | logging piles day by day, saving the journal between runs |
| `feature/test-suite` | `0cfbf28` | 96 tests, the fixtures, and the CSV decimal-comma fix they found |
| `refactor/mvp-architecture` | `7227733` | MVP layering, 159 tests, the rewritten README |

These four are *ancestors of `main`* — they are markers on a straight line, not
open work. Nothing is waiting to be merged.

> If you would rather mark finished history with **tags** (`git tag v0.1
> d389f4b`) and keep branches only for work in progress, that is the more
> conventional split, and these four can be converted with
> `git tag <name> <branch> && git branch -d <branch>`.

---

## Commits

Write the message for the person who runs `git log` in six months and is trying
to work out why something is the way it is.

- First line: what changed, in the imperative — *"Add work journal so piles can
  be logged day by day"*, not *"added journal"* or *"updates"*.
- Then a blank line, then **why**, and anything surprising: a bug you found, a
  trade-off you took, a thing you deliberately did not do.
- Say what you verified. *"Checked by running the rebuilt exe: it reopened the
  existing saved project unchanged"* is worth more than *"tested"*.

---

## Before a merge into `main`

```powershell
.\run.ps1 check
```

That builds **both Debug and Release** with warnings treated as errors, runs the
tests, and produces the `.exe`. It stops at the first failure, so it cannot
report success over a broken step. Release is built too because some warnings
only appear there — and Release is what the `.exe` people are given comes from.

Two things it cannot check for you:

- **If you touched anything the user sees**, start the built `.exe` and use it
  once. The test suite drives the presenter, not the window — a broken layout or
  an unwired button passes every test and is still broken.

- **If you touched `JsonProjectRepository`, `ProjectState`, or anything under
  `Model/`**, open an existing `projekt.mpali` with the new build first
  (`.\run.ps1 data` shows you where it is). That file holds weeks of site
  records that cannot be reconstructed from the inputs; a format change that
  silently fails to load is the worst thing this project can do.
