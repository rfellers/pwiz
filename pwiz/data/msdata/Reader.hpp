//
// Reader.hpp
//
//
// Original author: Darren Kessner <Darren.Kessner@cshs.org>
//
// Copyright 2008 Spielberg Family Center for Applied Proteomics
//   Cedars-Sinai Medical Center, Los Angeles, California  90048
//
// Licensed under the Apache License, Version 2.0 (the "License"); 
// you may not use this file except in compliance with the License. 
// You may obtain a copy of the License at 
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software 
// distributed under the License is distributed on an "AS IS" BASIS, 
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. 
// See the License for the specific language governing permissions and 
// limitations under the License.
//


#ifndef _READER_HPP_ 
#define _READER_HPP_ 

#include "pwiz/utility/misc/Export.hpp"
#include "MSData.hpp"
#include <string>
#include <stdexcept>


namespace pwiz {
namespace msdata {

/// interface for file readers
class PWIZ_API_DECL Reader
{
    public:


    /// return true iff Reader recognizes the file as one it should handle
	/// that's not to say one it CAN handle, necessarily, as in Thermo on linux,
	/// see comment for identify() below
    bool accept(const std::string& filename, 
                const std::string& head) const 
	{
		return (identify(filename,head).length() != 0);
	}

    /// return file type iff Reader recognizes the file, else empty; 
	/// note: for formats requiring a 3rd party DLL identify() should
	/// return true if it recognized the format, even though reading
	/// may fail if the 3rd party DLL isn't actually present
    /// Reader may filter based on filename and/or head of the file
    virtual std::string identify(const std::string& filename, 
                        const std::string& head) const = 0;

    /// fill in the MSData structure
    /// note: using this on a multi-run input file will throw an exception
    virtual void read(const std::string& filename, 
                      const std::string& head,
                      MSData& result) const = 0;

    /// fill in a vector of MSData structures; provides support for multi-run input files
    virtual void read(const std::string& filename, 
                      const std::string& head,
                      std::vector<MSDataPtr>& results) const;
    /// fill in a vector of MSData.Id values; provides support for multi-run input files
    virtual void readIds(const std::string& filename, 
                      const std::string& head,
                      std::vector<std::string>& dataIds) const;

	virtual const char *getType() const = 0; // what kind of reader are you?

    virtual ~Reader(){}
};

class PWIZ_API_DECL ReaderFail : public std::runtime_error // reader failure exception
{
    public:

    ReaderFail(const std::string& error)
    :   std::runtime_error(("[ReaderFail] " + error).c_str()),
		error_(error)        
    {}

    virtual const std::string& error() const {return error_;}
    virtual ~ReaderFail() throw() {}

    private:
    std::string error_;
};

typedef boost::shared_ptr<Reader> ReaderPtr;


///
/// Reader container (composite pattern).  
/// 
/// The template get<reader_type>() gives access to child Readers by type, to facilitate 
/// Reader-specific configuration at runtime. 
///
class PWIZ_API_DECL ReaderList : public Reader,
                                 public std::vector<ReaderPtr>
{
    public:

    /// returns child name iff some child identifies, else empty string
	virtual std::string identify(const std::string& filename) const; 

    /// returns child name iff some child identifies, else empty string
	virtual std::string identify(const std::string& filename, 
                                 const std::string& head) const; 

    /// delegates to first child that identifies
    virtual void read(const std::string& filename,
                      MSData& result) const;

    /// delegates to first child that identifies
    virtual void read(const std::string& filename, 
                      const std::string& head,
                      MSData& result) const;

    /// delegates to first child that identifies;
    /// provides support for multi-run input files
    virtual void read(const std::string& filename,
                      std::vector<MSDataPtr>& results) const;

    /// delegates to first child that identifies;
    /// provides support for multi-run input files
    virtual void read(const std::string& filename, 
                      const std::string& head,
                      std::vector<MSDataPtr>& results) const;

    /// delegates to first child that identifies;
    /// provides support for multi-run input files
    virtual void readIds(const std::string& filename,
                      std::vector<std::string>& results) const;

    /// delegates to first child that identifies;
    /// provides support for multi-run input files
    virtual void readIds(const std::string& filename, 
                      const std::string& head,
                      std::vector<std::string>& results) const;

    /// appends all of the rhs operand's Readers to the list
    ReaderList& operator +=(const ReaderList& rhs);

    /// appends the rhs Reader to the list
    ReaderList& operator +=(const ReaderPtr& rhs);

    /// returns a concatenated list of all the Readers from the lhs and rhs operands
    ReaderList operator +(const ReaderList& rhs) const;

    /// returns a concatenated list of all the Readers from the lhs and rhs operands
    ReaderList operator +(const ReaderPtr& rhs) const;

    /// returns pointer to Reader of the specified type
    template <typename reader_type>
    reader_type* get()
    {
        for (iterator it=begin(); it!=end(); ++it)
        {
            reader_type* p = dynamic_cast<reader_type*>(it->get());
            if (p) return p;
        }
        
        return 0;
    }

    /// returns const pointer to Reader of the specified type
    template <typename reader_type>
    const reader_type* get() const
    {
        return const_cast<ReaderList*>(this)->get<reader_type>();
    }

	virtual const char *getType() const {return "ReaderList";} // satisfy inheritance

};


/// returns a list containing the lhs and rhs as readers
PWIZ_API_DECL ReaderList operator +(const ReaderPtr& lhs, const ReaderPtr& rhs);


} // namespace msdata
} // namespace pwiz


#endif // _READER_HPP_ 

