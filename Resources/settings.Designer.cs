﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace Resources {
    using System;
    
    
    /// <summary>
    ///   A strongly-typed resource class, for looking up localized strings, etc.
    /// </summary>
    // This class was auto-generated by the StronglyTypedResourceBuilder
    // class via a tool like ResGen or Visual Studio.
    // To add or remove a member, edit your .ResX file then rerun ResGen
    // with the /str option, or rebuild your VS project.
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("System.Resources.Tools.StronglyTypedResourceBuilder", "15.0.0.0")]
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    public class settings {
        
        private static global::System.Resources.ResourceManager resourceMan;
        
        private static global::System.Globalization.CultureInfo resourceCulture;
        
        [global::System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        internal settings() {
        }
        
        /// <summary>
        ///   Returns the cached ResourceManager instance used by this class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        public static global::System.Resources.ResourceManager ResourceManager {
            get {
                if (object.ReferenceEquals(resourceMan, null)) {
                    global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("Resources.settings", typeof(settings).Assembly);
                    resourceMan = temp;
                }
                return resourceMan;
            }
        }
        
        /// <summary>
        ///   Overrides the current thread's CurrentUICulture property for all
        ///   resource lookups using this strongly typed resource class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        public static global::System.Globalization.CultureInfo Culture {
            get {
                return resourceCulture;
            }
            set {
                resourceCulture = value;
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Allow file formats for step upload.
        /// </summary>
        public static string FileExtensionsForStep {
            get {
                return ResourceManager.GetString("FileExtensionsForStep", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to File upload limit (MB).
        /// </summary>
        public static string FileUploadLimit {
            get {
                return ResourceManager.GetString("FileUploadLimit", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Amount of elements per page.
        /// </summary>
        public static string PageSize {
            get {
                return ResourceManager.GetString("PageSize", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Smtp message From attribute.
        /// </summary>
        public static string SmtpFrom {
            get {
                return ResourceManager.GetString("SmtpFrom", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Smtp server password.
        /// </summary>
        public static string SmtpPassword {
            get {
                return ResourceManager.GetString("SmtpPassword", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Smtp server port.
        /// </summary>
        public static string SmtpPort {
            get {
                return ResourceManager.GetString("SmtpPort", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Smtp server address.
        /// </summary>
        public static string SmtpServer {
            get {
                return ResourceManager.GetString("SmtpServer", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Smtp server username.
        /// </summary>
        public static string SmtpUsername {
            get {
                return ResourceManager.GetString("SmtpUsername", resourceCulture);
            }
        }
    }
}
