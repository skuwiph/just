## Synopsis

Generate markdown user story documents based on user-defined templates from specified Jira entries.

## Usage

1. Initialise the settings file:

        just init -p docs/stories -t s:\development\just\user_story_template.md

2. Create a new entry in the path specified by parameter `-p` using the markdown template specified by the parameter `-t`:

        just new rm-454

    Takes the title and user-description from the entry `https://aifsdevuk.atlassian.net/browse/rm-454` and merges it with the template specified in the `just-settings.json` file. The resultant file is opened in your system's registered editor for `.md` files. If you save this file, it will be saved in the path specified above, creating the folder if it doesn't already exist.



## Motivation

We of Jira for issue and user story tracking during development. However, we've found that even adding custom fields to Jira is a little too complex for what we want. By generating text files in a project-specific folder, we can include them in source control.

## Installation

Provide code examples and explanations of how to get the project.

## License

Licensed under the MIT license.

Certain components have been incorporated. Both are currently MIT licensed.

* [AngleSharp](https://github.com/AngleSharp/AngleSharp)

* [Json.Net](http://www.newtonsoft.com/json)


