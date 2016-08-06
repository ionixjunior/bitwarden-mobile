using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Bit.App.Abstractions;
using Bit.iOS.Extension.Models;
using Foundation;
using UIKit;
using XLabs.Ioc;
using Plugin.Settings.Abstractions;
using Bit.iOS.Core.Utilities;
using System.Threading.Tasks;
using Bit.iOS.Core;
using MobileCoreServices;
using Bit.iOS.Core.Controllers;

namespace Bit.iOS.Extension
{
    public partial class SiteListViewController : ExtendedUITableViewController
    {
        public SiteListViewController(IntPtr handle) : base(handle)
        { }

        public Context Context { get; set; }
        public LoadingViewController LoadingController { get; set; }

        public override void ViewWillAppear(bool animated)
        {
            UINavigationBar.Appearance.ShadowImage = new UIImage();
            UINavigationBar.Appearance.SetBackgroundImage(new UIImage(), UIBarMetrics.Default);
            base.ViewWillAppear(animated);
        }

        public async override void ViewDidLoad()
        {
            base.ViewDidLoad();
            if(!CanAutoFill())
            {
                CancelBarButton.Title = "Close";
            }

            TableView.RowHeight = UITableView.AutomaticDimension;
            TableView.EstimatedRowHeight = 44;
            TableView.Source = new TableSource(this);
            await ((TableSource)TableView.Source).LoadItemsAsync();
        }

        public bool CanAutoFill()
        {
            if(Context.ProviderType != Constants.UTTypeAppExtensionFillBrowserAction
                && Context.ProviderType != Constants.UTTypeAppExtensionFillWebViewAction
                && Context.ProviderType != UTType.PropertyList)
            {
                return true;
            }

            return Context.Details?.HasPasswordField ?? false;

        }

        partial void CancelBarButton_Activated(UIBarButtonItem sender)
        {
            LoadingController.CompleteRequest(null);
        }

        partial void AddBarButton_Activated(UIBarButtonItem sender)
        {
            PerformSegue("siteAddSegue", this);
        }

        public override void PrepareForSegue(UIStoryboardSegue segue, NSObject sender)
        {
            var navController = segue.DestinationViewController as UINavigationController;
            if(navController != null)
            {
                var addSiteController = navController.TopViewController as SiteAddViewController;
                if(addSiteController != null)
                {
                    addSiteController.Context = Context;
                    addSiteController.SiteListController = this;
                }
            }
        }

        public void DismissModal()
        {
            DismissViewController(true, async () =>
            {
                await ((TableSource)TableView.Source).LoadItemsAsync();
                TableView.ReloadData();
            });
        }

        public class TableSource : UITableViewSource
        {
            private const string CellIdentifier = "TableCell";

            private IEnumerable<SiteViewModel> _tableItems;
            private Context _context;
            private SiteListViewController _controller;

            public TableSource(SiteListViewController controller)
            {
                _context = controller.Context;
                _controller = controller;
            }

            public async Task LoadItemsAsync()
            {
                _tableItems = new List<SiteViewModel>();
                if(_context.DomainName != null)
                {
                    var siteService = Resolver.Resolve<ISiteService>();
                    var sites = await siteService.GetAllAsync();
                    var siteModels = sites.Select(s => new SiteViewModel(s));
                    _tableItems = siteModels
                        .Where(s => s.Domain != null && s.Domain.BaseDomain == _context.DomainName.BaseDomain)
                        .OrderBy(s => s.Name).ThenBy(s => s.Username)
                        .ToList();
                }
            }

            public IEnumerable<SiteViewModel> TableItems { get; set; }

            public override nint RowsInSection(UITableView tableview, nint section)
            {
                return _tableItems.Count() == 0 ? 1 : _tableItems.Count();
            }

            public override UITableViewCell GetCell(UITableView tableView, NSIndexPath indexPath)
            {
                if(_tableItems.Count() == 0)
                {
                    var noDataCell = new UITableViewCell(UITableViewCellStyle.Default, "NoDataCell");
                    noDataCell.TextLabel.Text = "There are no sites in your vault for this website. Tap to add one.";
                    noDataCell.TextLabel.TextAlignment = UITextAlignment.Center;
                    noDataCell.TextLabel.LineBreakMode = UILineBreakMode.WordWrap;
                    noDataCell.TextLabel.Lines = 0;
                    return noDataCell;
                }

                var cell = tableView.DequeueReusableCell(CellIdentifier);

                // if there are no cells to reuse, create a new one
                if(cell == null)
                {
                    Debug.WriteLine("BW Log, Make new cell for list.");
                    cell = new UITableViewCell(UITableViewCellStyle.Subtitle, CellIdentifier);
                    cell.DetailTextLabel.TextColor = cell.DetailTextLabel.TintColor = new UIColor(red: 0.47f, green: 0.47f, blue: 0.47f, alpha: 1.0f);
                }
                return cell;
            }

            public override void WillDisplay(UITableView tableView, UITableViewCell cell, NSIndexPath indexPath)
            {
                if(_tableItems.Count() == 0 || cell == null)
                {
                    return;
                }

                var item = _tableItems.ElementAt(indexPath.Row);
                cell.TextLabel.Text = item.Name;
                cell.DetailTextLabel.Text = item.Username;
            }

            public override void RowSelected(UITableView tableView, NSIndexPath indexPath)
            {
                tableView.DeselectRow(indexPath, true);
                tableView.EndEditing(true);

                if(_tableItems.Count() == 0)
                {
                    _controller.PerformSegue("siteAddSegue", this);
                    return;
                }

                Resolver.Resolve<ISettings>().AddOrUpdateValue(App.Constants.LastBackgroundedDate, DateTime.UtcNow);

                var item = _tableItems.ElementAt(indexPath.Row);
                if(item == null)
                {
                    _controller.LoadingController.CompleteRequest(null);
                    return;
                }

                if(_controller.CanAutoFill() && !string.IsNullOrWhiteSpace(item.Password))
                {
                    _controller.LoadingController.CompleteUsernamePasswordRequest(item.Username, item.Password);
                }
                else if(!string.IsNullOrWhiteSpace(item.Username) || !string.IsNullOrWhiteSpace(item.Password))
                {
                    var sheet = Dialogs.CreateActionSheet(item.Name, _controller);
                    if(!string.IsNullOrWhiteSpace(item.Username))
                    {
                        sheet.AddAction(UIAlertAction.Create("Copy Username", UIAlertActionStyle.Default, a =>
                        {
                            UIPasteboard clipboard = UIPasteboard.General;
                            clipboard.String = item.Username;
                            var alert = Dialogs.CreateMessageAlert("Copied username!");
                            _controller.PresentViewController(alert, true, () =>
                            {
                                _controller.DismissViewController(true, null);
                            });
                        }));
                    }

                    if(!string.IsNullOrWhiteSpace(item.Password))
                    {
                        sheet.AddAction(UIAlertAction.Create("Copy Password", UIAlertActionStyle.Default, a =>
                        {
                            UIPasteboard clipboard = UIPasteboard.General;
                            clipboard.String = item.Password;
                            var alert = Dialogs.CreateMessageAlert("Copied password!");
                            _controller.PresentViewController(alert, true, () =>
                            {
                                _controller.DismissViewController(true, null);
                            });
                        }));
                    }

                    sheet.AddAction(UIAlertAction.Create("Cancel", UIAlertActionStyle.Cancel, null));
                    _controller.PresentViewController(sheet, true, null);
                }
                else
                {
                    var alert = Dialogs.CreateAlert(null, "This site does not have a username or password configured.", "Ok");
                    _controller.PresentViewController(alert, true, null);
                }
            }
        }
    }
}
