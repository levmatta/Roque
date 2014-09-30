using System.Configuration;

namespace Cinchcast.Roque.Core.Configuration
{
    using System;
    using System.Linq;

    /// <summary>
    /// Subscriber collection config element
    /// </summary>
    public class SubscriberCollection : ConfigurationElementCollection
    {
        public SubscriberElement this[object key]
        {
            get
            {
                return base.BaseGet(key) as SubscriberElement;
            }
        }

        public override ConfigurationElementCollectionType CollectionType
        {
            get
            {
                return ConfigurationElementCollectionType.BasicMap;
            }
        }

        protected override string ElementName
        {
            get
            {
                return "subscriber";
            }
        }

        protected override bool IsElementName(string elementName)
        {
            bool isName = false;
            if (!String.IsNullOrEmpty(elementName))
                isName = elementName.Equals("subscriber");
            return isName;
        }

        protected override ConfigurationElement CreateNewElement()
        {
            return new SubscriberElement();
        }

        protected override object GetElementKey(ConfigurationElement element)
        {
            return ((SubscriberElement)element).SubscriberType;
        }
    }
}
