import React, { Children, isValidElement } from "react";

interface StatCardProps {
  title: string;
  children: React.ReactNode;
  blur?: boolean;
  className?: string;
}

export const StatCard: React.FC<StatCardProps> = ({ title, children, blur, className }) => {
  let mainContentElements: React.ReactNode[] = [];
  let footerElement: React.ReactElement<StatCardFooterProps> | null = null;
  let footerBlur: boolean | undefined = undefined;

  Children.forEach(children, (child) => {
    if (isValidElement<StatCardFooterProps>(child) && child.type === StatCardFooter) {
      footerElement = child;
      footerBlur = child.props.blur;
    } else {
      mainContentElements.push(child);
    }
  });

  return (
    <div className={`min-w-0 flex-1 basis-0 ${className || ""}`.trim()}>
      <div className="text-gray-500">{title}</div>
      <div className={blur ? "blur-sm" : ""}>{mainContentElements}</div>
      {footerElement &&
        React.cloneElement(footerElement, {
          blur: footerBlur ?? blur,
        })}
    </div>
  );
};

interface StatCardFooterProps {
  children: React.ReactNode;
  blur?: boolean;
}

export const StatCardFooter: React.FC<StatCardFooterProps> = ({ children, blur }) => {
  return <div className={`text-xs text-gray-500 ${blur ? "blur-sm" : ""}`}>{children}</div>;
};
