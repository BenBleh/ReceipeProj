export function StepLayout({steps})
{
 return (   
    <>
        {
            steps ?
            steps.map(step  => {
                return (                    
                    <>
                        <h3>Step {step.num + 1}</h3>                        
                        <label>{step.instructions}</label>
                    </>                    
                )
            })
            : ""
        }           
    </>  
 )
}