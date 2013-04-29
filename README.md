umbraco.webservices
===================

Following the security issue [described here](http://umbraco.com/follow-us/blog-archive/2013/4/29/security-vulnerability-found-immediate-action-recommended.aspx "described here"), the umbraco.webservices.dll has been split out of the Umbraco source code and put into this seperate project.

This version has been secured so it is once again safe to use.

After building the project, you'll find the necessary files in the /Release folder of this project. Copy the two folders to the root of your Umbraco site and you should be good to go.