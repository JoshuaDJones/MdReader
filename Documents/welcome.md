# Welcome to MdReader

MdReader is a small, offline library for documents written in Markdown. Every document is bundled into the Windows and Android apps, so no internet connection or account is required.

## Adding your own document

Drag any `.md` file into the repository's `Documents` folder and rebuild the application. MdReader discovers it automatically—there is no catalog to update.

The document's first level-one heading becomes its library title:

```markdown
# My Document Title
```

The first regular paragraph becomes its description. If there is no level-one heading, MdReader creates a title from the filename.

## Organizing documents

You can create subfolders inside `Documents`. A subfolder's name becomes the category displayed on its document cards.

```text
Documents/
├── welcome.md
├── Recipes/
│   └── bread.md
└── Work Notes/
    └── project-plan.md
```

## Reading documents

Select any card in the library to open it. While reading, use **A−** and **A+** in the toolbar to adjust the text size. The reader also follows your device's light or dark appearance automatically.
