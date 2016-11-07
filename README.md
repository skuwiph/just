## Synopsis

Generate markdown user story documents based on user-defined templates from specified Jira entries.

## Motivation

We use Jira for issue and user story tracking during development. 
However, we've found that even adding custom fields to Jira is a little too complex for what we want.
By generating text files in a project-specific folder, we can include them in source control and easily add extra details as the story evolves.

## Usage

1. Initialise the settings file:

        just init -p docs/stories -t s:\development\just\user_story_template.md -j jira_anchor_uri

	Where:
		 -p specifies where any created stories will be saved
		 -t specifies the template document can be found (UNC or network share paths are valid)
		 -u specifies the Jira anchor uri for your Jira site (e.g. company.atlassian.net)

2. Set your user details:

		just setuser myusername:mypassword

	Stores a base-64 encoded string under your Windows %APPDATA% folder. If you do not wish to store these details, you can pass them directly when creating a new story.

3. Create a new story:

        just new rm-454 [-u username:password]

    Reads various details from the Jira REST API using either the stored or passed authentication details and merges them the template specified in the `just-settings.json` file. The resultant file is opened in your system's registered editor for `.md` files. If you save this file, it will be saved in the path specified above, creating the folder if it doesn't already exist.

### Template format

The template will replace the following strings during the edit phase:

* _TITLE 
* _URL
* _DESCRIPTION
* _CREATEDBY
* _DATE

As read from the Jira document

## Installation

Copy the compiled .exe to your path

## License

Licensed under the MIT license.

Certain components have been incorporated. Both are currently MIT licensed.

* [AngleSharp](https://github.com/AngleSharp/AngleSharp)
* [Json.Net](http://www.newtonsoft.com/json)

## TODO

1. Specify Jira URL document fields to extract during initialisation step?
2. Add markdown editor option to settings file and/or environment read 
3. Specify whether to use HTML or JSON in reading from Jira
