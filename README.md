<a href="http://www.crownpeak.com" target="_blank">![Crownpeak Logo](images/logo/crownpeak-logo.png?raw=true "Crownpeak Logo")</a>

# Crownpeak Content Xcelerator℠
Crownpeak Content Xcelerator℠ (“Content Xcelerator℠”) is made available under the MIT License (“License”) at [https://github.com/Crownpeak/Content-Xcelerator](https://github.com/Crownpeak/Content-Xcelerator), as optional tooling to assist developers working with content migration into Crownpeak Digital Experience Management (“DXM”).

Constructed as a set of Microsoft .NET Visual Studio Projects, producing a Microsoft Windows Forms Application and set of related Microsoft .NET Assemblies, Content Xcelerator℠ can be used “as is” to migrate content between DXM “Instances”, or forked and modified as required to provide further capabilities, within the terms of the License.

Content Xcelerator℠ is not actively maintained by Crownpeak. The underlying DXM Access API (“Access API”), invoked by Content Xcelerator℠, forms part of the DXM product itself, which is fully maintained by Crownpeak.

## Executing Content Xcelerator℠
To run Content Xcelerator℠, execute the Microsoft Windows Executable from within a Microsoft Windows environment.
![Executing Content Xcelerator℠](images/screenshots/content-xcelerator-executing.png?raw=true "Executing Content Xcelerator℠")

## Exporting Data from DXM
Upon first execution of Content Xcelerator℠, you are required to enter configuration information and your personal security credentials, as follows:

* **Sessions** – Historically stored configurations – this will be blank upon first run;
* **Server** – The URL to the DXM Server farm, typically “cms.crownpeak.net”;
* **Instance** – The DXM Instance that you wish to target;
* **Username** – Your DXM username, typically your email address;
* **Password** – Your DXM password;
* **Developer Key** – Your personal DXM Developer Key. You should contact Crownpeak Support (support@crownpeak.com) if you do not have this.

**N.B.** Content Xcelerator℠ does not support Federated Authentication. A forms-based user account, with sufficient permission, must be provided.

![Content Xcelerator℠ Export Settings](images/screenshots/content-xcelerator-export-settings.png?raw=true "Content Xcelerator℠ Export Settings")

Select the top-level Asset Folder that you wish to export content for. By default, all child Asset Folders and Assets will be selected.

![Content Xcelerator℠ Select Assets](images/screenshots/content-xcelerator-export-assets.png?raw=true "Content Xcelerator℠ Select Assets")

**N.B.** You will not be able to proceed to the next step, until the DXM content tree has completed loading.

Choose an appropriate location for your export XML file and start the export process.

![Content Xcelerator℠ Export Process](images/screenshots/content-xcelerator-export-process.png?raw=true "Content Xcelerator℠ Export Process")

**N.B.** You can persist the log content to disk, using the “Save Log”.

Once complete, an XML document will have been created at your chosen location. This XML document contains everything that you need in order to import this content into another DXM location.

![Content Xcelerator℠ Export Result](images/screenshots/content-xcelerator-export-result.png?raw=true "Content Xcelerator℠ Export Result")

## Importing Data to DXM
Upon first execution of Content Xcelerator℠, you are required to enter configuration information and your personal security credentials, as follows:

* **Sessions** – Historically stored configurations – this will be blank upon first run;
* **Server** – The URL to the DXM Server farm, typically “cms.crownpeak.net”;
* **Instance** – The DXM Instance that you wish to target;
* **Username** – Your DXM username, typically your email address;
* **Password** – Your DXM password;
* **Developer Key** – Your personal DXM Developer Key. You should contact Crownpeak Support (support@crownpeak.com) if you do not have this.

**N.B.** Content Xcelerator℠ does not support Federated Authentication. A forms-based user account, with sufficient permission, must be provided.

![Content Xcelerator℠ Import Settings](images/screenshots/content-xcelerator-import-settings.png?raw=true "Content Xcelerator℠ Import Settings")

Select the XML file from your local machine that represents the content that you wish to import. Once selected, click “Refresh” to load the content of the file into the dialog. Choose which content you would like to import into DXM. By default, all child Asset Folders and Assets will be selected.

![Content Xcelerator℠ Select Assets](images/screenshots/content-xcelerator-import-assets.png?raw=true "Content Xcelerator℠ Select Assets")

Content Xcelerator℠ will parse the XML file contents and compare it against the target DXM configuration, to try to assess any potential import issues, prior to executing the import process. Any identified issues will be detailed within the dialog box.

![Content Xcelerator℠ Review Problems](images/screenshots/content-xcelerator-import-problems.png?raw=true "Content Xcelerator℠ Review Problems")

**N.B.** It is possible to skip items with issues, or to re-map problem items to other suitable Assets within DXM (e.g. a missing template could be re-mapped to another template. This is only possible to items within DXM already, not to items within the current import file).

Type the “Top” or “Parent” Asset Folder where you want to deploy the exported content.

![Content Xcelerator℠ Import Process](images/screenshots/content-xcelerator-import-process.png?raw=true "Content Xcelerator℠ Import Process")

Upon completion, navigate to the relevant DXM UI and review your imported content.

![Content Xcelerator℠ Import Result](images/screenshots/content-xcelerator-import-result.png?raw=true "Content Xcelerator℠ Import Result")