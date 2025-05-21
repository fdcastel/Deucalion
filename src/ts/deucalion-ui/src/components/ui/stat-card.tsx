import React, { Children, isValidElement } from "react";

interface StatCardProps {
  title: string;
  children: React.ReactNode;
  blur?: boolean;
  className?: string;
}

export const StatCard: React.FC<StatCardProps> = ({ title, children, blur, className }) => {
  let mainContentElements: React.ReactNode[] = [];
  let footerElement: React.ReactNode = null;

  Children.forEach(children, (child) => {
    if (isValidElement(child) && child.type === StatCardFooter) {
      footerElement = child;
    } else {
      mainContentElements.push(child);
    }
  });

  return (
    <div className={`min-w-0 flex-1 basis-0 ${className || ""}`.trim()}>
      <div className="text-gray-500">{title}</div>
      <div className={blur ? "blur-sm" : ""}>{mainContentElements}</div>
      {footerElement}
    </div>
  );
};

interface StatCardFooterProps {
  children: React.ReactNode;
}

export const StatCardFooter: React.FC<StatCardFooterProps> = ({ children }) => {
  return <div className="text-gray-500 text-xs">{children}</div>;
};
